using System.Text.Json;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Harness.Invoke;
using Xunit;

namespace LambdaWidgets.Harness.Tests;

/// <summary>
/// The console, locally: what the harness sends a widget and what it does with the answer.
///
/// <para>
/// The target is a double rather than a running emulator, because what is under test is the
/// harness's behaviour and not the Invoke API's. What the double records is the payload, which is
/// the thing a widget author is really asking about when they open the inspector.
/// </para>
/// </summary>
public class WhatTheHarnessDoesTests {
    private const string Body = """
        {
          "widgets": [
            {
              "type": "custom",
              "properties": {
                "endpoint": "arn:aws:lambda:us-east-1:012345678901:function:customWidgetEcho",
                "params": { "echo": "<h1>hi</h1>" }
              }
            }
          ]
        }
        """;

    // ------------------------------------------------------------------ opening

    [Fact]
    public async Task OpeningAWidgetSendsItsConfiguredParameters() {
        var target = new RecordingTarget("\"<h1>hi</h1>\"");

        await Harness(target).Open("widget-1", TestContext.Current.CancellationToken);

        Assert.Contains("\"echo\":\"\\u003Ch1\\u003Ehi", target.LastPayload);
    }

    /// <summary>
    /// A dashboard holds widgets served by different things at once, so the target is chosen per
    /// widget from its own endpoint rather than from a setting.
    /// </summary>
    [Fact]
    public async Task TheFunctionNameComesFromTheWidgetsEndpoint() {
        var target = new RecordingTarget("\"<p>ok</p>\"");

        await Harness(target).Open("widget-1", TestContext.Current.CancellationToken);

        Assert.Equal("customWidgetEcho", target.LastFunction);
    }

    [Fact]
    public async Task WhatTheWidgetAnsweredIsWhatIsShown() {
        var view = await Harness(new RecordingTarget("\"<h1>hi</h1>\"")).Open("widget-1", TestContext.Current.CancellationToken);

        Assert.Equal(ResponseKind.Html, view.Shown.Kind);
        Assert.Contains("<h1>hi</h1>", view.Shown.Html);
    }

    // ------------------------------------------------------------------ clicking

    [Fact]
    public async Task ClickingSendsTheActionsRouteAndTheWidgetsForms() {
        var target = new RecordingTarget("""
            "<input name=\"query\" value=\"typed\"><a class=\"btn\">Run</a><cwdb-action action=\"call\" endpoint=\"arn:aws:lambda:us-east-1:1:function:customWidgetEcho\">{\"route\":\"/search\"}</cwdb-action>"
            """);

        var harness = Harness(target);

        await harness.Open("widget-1", TestContext.Current.CancellationToken);
        await harness.Click("widget-1", 0, new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        Assert.Contains("\"route\":\"/search\"", target.LastPayload);
        Assert.Contains("\"query\":\"typed\"", target.LastPayload);
    }

    /// <summary>
    /// A widget that has not rendered has no action to fire, and saying so beats an index error.
    /// </summary>
    [Fact]
    public async Task ClickingAWidgetThatHasNotRenderedIsRefused() {
        var harness = Harness(new RecordingTarget("\"<p>ok</p>\""));

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Click("widget-1", 0, new Dictionary<string, string>(), TestContext.Current.CancellationToken));

        Assert.Contains("has not been rendered", refused.Message);
    }

    // ------------------------------------------------------------------ refreshing

    /// <summary>
    /// The behaviour the harness exists to reproduce. A refresh re-invokes with the widget's
    /// configured parameters, so a viewer three pages in is returned to the landing page — which an
    /// author who only meets it in production has already shipped.
    /// </summary>
    [Fact]
    public async Task ARefreshReturnsAWidgetToItsLandingPage() {
        var target = new RecordingTarget("""
            "<a class=\"btn\">Next</a><cwdb-action action=\"call\" endpoint=\"arn:aws:lambda:us-east-1:1:function:customWidgetEcho\">{\"route\":\"/page-2\"}</cwdb-action>"
            """);

        var harness = Harness(target);

        await harness.Open("widget-1", TestContext.Current.CancellationToken);
        await harness.Click("widget-1", 0, new Dictionary<string, string>(), TestContext.Current.CancellationToken);

        Assert.Contains("/page-2", target.LastPayload);

        await harness.RefreshAll(DashboardTrigger.Refresh, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("route", target.LastPayload);
    }

    /// <summary>A widget that turned a trigger off is not re-invoked by it.</summary>
    [Fact]
    public async Task AWidgetThatOptedOutOfATriggerIsNotReinvoked() {
        var target = new RecordingTarget("\"<p>ok</p>\"");

        var harness = Harness(target, """
            {"widgets":[{"type":"custom","properties":{
                "endpoint":"arn:aws:lambda:us-east-1:1:function:f",
                "updateOn":{"refresh":false,"resize":true,"timeRange":true}}}]}
            """);

        await harness.RefreshAll(DashboardTrigger.Refresh, TestContext.Current.CancellationToken);

        Assert.Equal(0, target.Calls);
    }

    // ------------------------------------------------------------------ failure

    /// <summary>
    /// A function that threw answers 200 with X-Amz-Function-Error set. Reading the status alone
    /// would report every crashed widget as a success, and the console shows the viewer nothing
    /// useful — showing the payload is most of why developing here beats developing on a dashboard.
    /// </summary>
    [Fact]
    public async Task AFunctionThatThrewIsReportedRatherThanRendered() {
        var target = new RecordingTarget("""{"errorMessage":"boom"}""") { FunctionError = "Unhandled" };

        var view = await Harness(target).Open("widget-1", TestContext.Current.CancellationToken);

        Assert.True(view.Invoke.Failed);
        Assert.Contains("boom", view.Invoke.Body);
    }

    /// <summary>
    /// The Invoke API answers 200 when the function itself threw, and says so in a header. Reading
    /// the status alone reports every crashed widget as a success.
    ///
    /// <para>
    /// Through <see cref="WidgetInvoker"/> against a real HTTP response rather than through the
    /// double above, which was the gap: the double sets the error itself, so a mutation that never
    /// read the header passed every test until this one existed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TheFunctionErrorHeaderIsWhatSaysAFunctionThrew() {
        var invoker = new WidgetInvoker(
            new HttpClient(new AnsweringHandler("""{"errorMessage":"boom"}""", "Unhandled")),
            new InvokeRouting(InvokeTarget.TestTool()));

        var result = await invoker.Invoke("customWidgetEcho", "{}", TestContext.Current.CancellationToken);

        Assert.True(result.Failed);
        Assert.Equal("Unhandled", result.FunctionError);
    }

    /// <summary>And a function that answered normally is not reported as having failed.</summary>
    [Fact]
    public async Task AnAnswerWithNoErrorHeaderIsASuccess() {
        var invoker = new WidgetInvoker(
            new HttpClient(new AnsweringHandler("\"<p>ok</p>\"", functionError: null)),
            new InvokeRouting(InvokeTarget.TestTool()));

        Assert.False((await invoker.Invoke("customWidgetEcho", "{}", TestContext.Current.CancellationToken)).Failed);
    }

    /// <summary>
    /// The name in the path is what the emulator routes on, so a widget served by one target does
    /// not reach another's function.
    /// </summary>
    [Fact]
    public async Task TheInvokeGoesToTheFunctionsOwnPath() {
        var handler = new AnsweringHandler("\"<p>ok</p>\"", functionError: null);

        await new WidgetInvoker(new HttpClient(handler), new InvokeRouting(InvokeTarget.TestTool(5050)))
            .Invoke("customWidgetEcho", "{}", TestContext.Current.CancellationToken);

        Assert.Equal(
            "http://localhost:5050/2015-03-31/functions/customWidgetEcho/invocations",
            handler.LastUri?.ToString());
    }

    /// <summary>
    /// A target named for one function wins over the default, so a dashboard can hold widgets
    /// served by different things at once.
    /// </summary>
    [Fact]
    public void AnExplicitlyNamedFunctionWinsOverTheDefault() {
        var routing = InvokeRouting.From(
            ["--test-tool", "5050", "--function", "customWidgetSearch=http://localhost:9000"]);

        Assert.Equal("http://localhost:9000", routing.For("customWidgetSearch").Address);
        Assert.Equal("http://localhost:5050", routing.For("customWidgetEcho").Address);
    }

    /// <summary>
    /// The emulator serves one function per container and does not route by name, so a widget whose
    /// endpoint names something else still reaches it.
    /// </summary>
    [Fact]
    public void TheRuntimeEmulatorIgnoresTheFunctionName() {
        Assert.Equal(
            "http://localhost:9000/2015-03-31/functions/function/invocations",
            InvokeTarget.Rie("http://localhost:9000").UriFor("customWidgetEcho").ToString());
    }

    /// <summary>
    /// A target that is not listening is the ordinary first-run failure, so the answer names the
    /// address tried and what it was configured for rather than throwing out of the page.
    /// </summary>
    [Fact]
    public async Task ATargetThatIsNotListeningNamesItself() {
        var invoker = new WidgetInvoker(
            new HttpClient(new RefusingHandler()), new InvokeRouting(InvokeTarget.TestTool(5050)));

        var result = await invoker.Invoke("customWidgetEcho", "{}", TestContext.Current.CancellationToken);

        Assert.True(result.Failed);
        Assert.Contains("localhost:5050", result.Body);
        Assert.Contains("AWS Lambda Test Tool", result.Body);
    }

    private static IWidgetHarness Harness(RecordingTarget target, string body = Body) {
        var console = new WidgetConsole(
            new WidgetActions(), new WidgetForms(), new WidgetSanitizer(),
            new WidgetStyles(), new WidgetResponses(), new WidgetEvents());

        return new WidgetHarness(console, target, new DashboardBodies().Read(body));
    }

    /// <summary>A target that answers with whatever it was given, and remembers what it was sent.</summary>
    private sealed class RecordingTarget(string answer) : IWidgetInvoker {
        public string? LastPayload { get; private set; }

        public string? LastFunction { get; private set; }

        public string? FunctionError { get; init; }

        public int Calls { get; private set; }

        public Task<InvokeResult> Invoke(
            string functionName, string payload, CancellationToken cancellationToken) {
            LastFunction = functionName;
            LastPayload = payload;
            Calls++;

            return Task.FromResult(new InvokeResult(answer, FunctionError, TimeSpan.Zero));
        }
    }

    private sealed class AnsweringHandler(string body, string? functionError) : HttpMessageHandler {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) {
            LastUri = request.RequestUri;

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                Content = new StringContent(body)
            };

            if (functionError is not null) {
                response.Headers.Add(WidgetInvoker.FunctionErrorHeader, functionError);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class RefusingHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
