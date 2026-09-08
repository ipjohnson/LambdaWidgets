using System.Text.Json;
using Amazon.CDK;
using Amazon.CDK.Assertions;
using Deploy;
using Xunit;

namespace Deploy.Tests;

/// <summary>
/// What the stack would create, read out of the synthesized template.
///
/// <para>
/// Infrastructure is testable without deploying it, and the parts worth testing are the ones a
/// reviewer cannot see at a glance: whether the role is scoped, whether the dashboard names the
/// functions that were actually created, and whether the runtime matches what the binary is. A
/// template assertion catches all three before an account does.
/// </para>
/// </summary>
public class DeployingWidgetsTests {
    private static Template Synthesized() {
        // Assets are not published in a test run, so the stack is synthesized with the folders
        // absent. CDK stages whatever is there; the template is what is under test, not the bundle.
        Directory.CreateDirectory("../LogsSearch/bin/Release/net8.0/linux-arm64/publish");
        Directory.CreateDirectory("../Echo/bin/Release/net8.0/linux-arm64/publish");

        var app = new App();

        return Template.FromStack(new WidgetStack(app, "Test"));
    }

    // ------------------------------------------------------------------ the functions

    /// <summary>
    /// A native binary deploys as its own executable: the runtime starts it and it talks the
    /// Runtime API itself, so the handler is the assembly name rather than a type and a method.
    /// </summary>
    [Fact]
    public void AWidgetDeploysAsANativeExecutable() {
        Synthesized().HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object> {
            ["Runtime"] = "provided.al2023",
            ["Handler"] = "customWidgetLogsSearch",
            ["Architectures"] = new[] { "arm64" }
        });
    }

    /// <summary>
    /// Adding a custom widget means picking a function out of a list of every function in the
    /// account. The prefix AWS recommends is the only signal a dashboard author has about which
    /// ones are meant for it.
    /// </summary>
    [Theory]
    [InlineData("customWidgetLogsSearch")]
    [InlineData("customWidgetEcho")]
    public void EveryWidgetFunctionCarriesThePrefixTheConsoleLooksFor(string name) {
        Synthesized().HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object> {
            ["FunctionName"] = name
        });
    }

    /// <summary>
    /// A widget answers while a viewer waits, so it needs a timeout a page load can live with
    /// rather than Lambda's three seconds or its fifteen minutes.
    /// </summary>
    [Fact]
    public void AWidgetAnswersWithinAPageLoad() {
        Synthesized().HasResourceProperties("AWS::Lambda::Function", new Dictionary<string, object> {
            ["FunctionName"] = "customWidgetLogsSearch",
            ["Timeout"] = 30
        });
    }

    // ------------------------------------------------------------------ the role

    /// <summary>
    /// The role is the whole of what the tool may do, and it is why a widget is safer than the
    /// script it replaces: that script runs with whatever its author's credentials allow.
    /// </summary>
    [Fact]
    public void TheSearchWidgetsRoleAllowsOnlyTheQueriesItMakes() {
        var granted = Statements(Synthesized())
            .Where(statement => statement.Contains("logs:", StringComparison.Ordinal))
            .ToList();

        var allowed = Assert.Single(granted);

        Assert.Contains("logs:StartQuery", allowed);
        Assert.Contains("logs:GetQueryResults", allowed);
        Assert.Contains("logs:StopQuery", allowed);
        Assert.DoesNotContain("logs:DeleteLogGroup", allowed);
        Assert.DoesNotContain("logs:PutLogEvents", allowed);
    }

    /// <summary>
    /// Echo reads nothing, so it is granted nothing beyond writing its own logs. A sample that
    /// asked for permissions it does not use would teach the opposite of the point.
    /// </summary>
    [Fact]
    public void TheEchoWidgetIsGrantedNothingItDoesNotUse() {
        Assert.DoesNotContain(
            Statements(Synthesized()),
            statement => statement.Contains("dynamodb:", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ the dashboard

    /// <summary>
    /// <b>One artifact.</b> The dashboard the harness renders locally is the dashboard that gets
    /// deployed, with the placeholder endpoints filled in. Authoring the deployed one separately
    /// would be two dashboards that drift.
    /// </summary>
    [Fact]
    public void TheDeployedDashboardIsTheOneDevelopedAgainst() {
        var body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "dashboard.json"));

        using var document = JsonDocument.Parse(body);

        var widgets = document.RootElement.GetProperty("widgets");

        Assert.Equal(2, widgets.GetArrayLength());
        Assert.All(widgets.EnumerateArray(), widget =>
            Assert.Equal("custom", widget.GetProperty("type").GetString()));
    }

    /// <summary>
    /// The placeholder ARNs carry the account of nowhere, so a body still holding one would point
    /// a viewer's console at a function that does not exist.
    /// </summary>
    [Fact]
    public void NoPlaceholderEndpointSurvivesIntoTheTemplate() {
        Assert.DoesNotContain("000000000000", Json(Synthesized()));
    }

    // ------------------------------------------------------------------ who may use it

    /// <summary>
    /// One policy a viewer's role attaches, rather than a permission per person. It is not
    /// sufficient on its own - the console will not run a custom widget until the viewer allows it
    /// - but without it the widget cannot run at all.
    /// </summary>
    [Fact]
    public void ViewersAreGrantedInvokeAndNothingElseOnTheFunctions() {
        var granted = Statements(Synthesized())
            .Where(statement => statement.Contains("lambda:", StringComparison.Ordinal))
            .ToList();

        var allowed = Assert.Single(granted);

        Assert.Contains("lambda:InvokeFunction", allowed);
        Assert.DoesNotContain("lambda:UpdateFunctionCode", allowed);
    }

    /// <summary>
    /// Every IAM statement in the template, as text.
    /// </summary>
    /// <remarks>
    /// <c>Template.ToJSON()</c> answers a dictionary rather than a string — its <c>ToString()</c>
    /// is the type name, which parses as JSON exactly as well as it sounds. Serializing the
    /// dictionary is what turns it into something assertable.
    /// </remarks>
    private static IReadOnlyList<string> Statements(Template template) =>
        JsonDocument.Parse(Json(template)).RootElement.GetProperty("Resources").EnumerateObject()
            .Where(resource => resource.Value.GetProperty("Type").GetString()
                is "AWS::IAM::Policy" or "AWS::IAM::ManagedPolicy")
            .Select(resource => resource.Value.GetProperty("Properties").GetRawText())
            .ToList();

    private static string Json(Template template) => JsonSerializer.Serialize(template.ToJSON());
}
