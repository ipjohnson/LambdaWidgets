using Xunit;

namespace LambdaWidgets.Dashboard.Tests;

/// <summary>
/// The parts of the contract where the implementation is the requirement: what an absent attribute
/// means, what an ARN resolves to, and which mistakes an author gets told about.
///
/// <para>
/// Direct rather than through <see cref="IWidgetConsole"/>, because there is no composition to
/// exercise. "What does the console strip" has one answer and one place it lives.
/// </para>
/// </summary>
public class ConsoleContractTests {
    private static readonly IWidgetActions Actions = new WidgetActions();

    // ------------------------------------------------------------------ the documented defaults

    /// <summary>
    /// A parsed action with the attribute absent takes <c>default</c>, so the enum's first member
    /// has to be the console's documented default or every unattributed action behaves wrongly.
    /// </summary>
    [Fact]
    public void AnActionWithNoAttributesDisplaysHtmlInTheWidgetOnClick() {
        var action = Assert.Single(Actions.In("<a>x</a><cwdb-action>hi</cwdb-action>"));

        Assert.Equal(ActionKind.Html, action.Kind);
        Assert.Equal(Display.Widget, action.Display);
        Assert.Equal(ActionEvent.Click, action.Event);
    }

    // ------------------------------------------------------------------ binding

    /// <summary>
    /// The rule the whole contract rests on. Whitespace and comments between the element and the
    /// action do not break the binding; another element does.
    /// </summary>
    [Fact]
    public void AnActionBindsTheElementImmediatelyBeforeIt() {
        var action = Assert.Single(Actions.In(
            "<a>Reboot</a>\n  <!-- why -->\n  <cwdb-action>hi</cwdb-action>"));

        Assert.Equal("Reboot", action.Text);
        Assert.Empty(action.Faults);
    }

    /// <summary>
    /// Renders, and does nothing when clicked. The console reports none of these, which is why the
    /// linter and the test driver read them.
    /// </summary>
    [Theory]
    [InlineData("<cwdb-action>hi</cwdb-action>", ActionFault.NoBoundElement)]
    [InlineData("""<a>x</a><cwdb-action action="call">{}</cwdb-action>""", ActionFault.CallWithoutEndpoint)]
    [InlineData("""<a>x</a><cwdb-action action="call" endpoint="e" event="mouseenter">{}</cwdb-action>""", ActionFault.MouseEnterOnCall)]
    [InlineData("""<a>x</a><cwdb-action action="call" endpoint="e">not json</cwdb-action>""", ActionFault.CallContentIsNotJson)]
    public void AFaultTheConsoleSwallowsIsReported(string html, ActionFault expected) {
        Assert.Contains(expected, Assert.Single(Actions.In(html)).Faults);
    }

    /// <summary>
    /// An action naming only a route sends no parameters, which is not the same as unparseable
    /// content and must not be reported as one.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    public void ACallWithNoParametersIsNotAFault(string content) {
        Assert.Empty(Assert.Single(Actions.In(
            $"""<a>x</a><cwdb-action action="call" endpoint="e">{content}</cwdb-action>""")).Faults);
    }

    // ------------------------------------------------------------------ choosing an invoke target

    /// <summary>
    /// The segment after <c>function:</c> is what selects a target in the harness. An alias or
    /// version qualifier follows the name and is not part of it.
    /// </summary>
    [Theory]
    [InlineData("arn:aws:lambda:us-east-1:1:function:customWidgetEcho", "customWidgetEcho")]
    [InlineData("arn:aws:lambda:us-east-1:1:function:customWidgetEcho:live", "customWidgetEcho")]
    [InlineData("customWidgetEcho", "customWidgetEcho")]
    public void TheFunctionNameIsWhatSelectsAnInvokeTarget(string endpoint, string expected) {
        var action = Assert.Single(Actions.In(
            $$"""<a>x</a><cwdb-action action="call" endpoint="{{endpoint}}">{}</cwdb-action>"""));

        Assert.Equal(expected, action.FunctionName);
    }

    // ------------------------------------------------------------------ documentation

    /// <summary>
    /// The console lifts the first fenced yaml block into the widget's parameters editor. Later
    /// blocks document other things and are left alone.
    /// </summary>
    [Fact]
    public void TheFirstFencedYamlBlockBecomesTheWidgetsParameters() {
        var parameters = new WidgetDocumentation().Parameters(
            """
            ## Log search

            ```yaml
            logGroups: /aws/lambda/orders
            limit: 20
            ```

            ```yaml
            notThese: true
            ```
            """);

        Assert.Equal("logGroups: /aws/lambda/orders\nlimit: 20", parameters);
    }

    [Fact]
    public void DescribeMarkdownWithNoYamlBlockOffersNoParameters() {
        Assert.Null(new WidgetDocumentation().Parameters("## Log search\n\nNo parameters."));
    }
}
