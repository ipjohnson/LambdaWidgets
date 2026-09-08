using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Hardened.Aws.Lambda.Runtime.Hosting;
using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using Xunit;

namespace Echo.Tests;

/// <summary>
/// The Echo widget, driven the way the console drives it.
///
/// <para>
/// This is the sample's own test and the framework's integration test at once: every piece of
/// <c>LambdaWidgets.Runtime</c> is in the path — the adapter, the merge, the context, the view base,
/// the helpers and describe — and none of it is named here. What is asserted is what a viewer would
/// see.
/// </para>
/// </summary>
public class EchoTests {
    private static readonly IWidgetResponses Responses = new WidgetResponses();
    private static readonly IWidgetSanitizer Sanitizer = new WidgetSanitizer();

    // ------------------------------------------------------------------ rendering

    /// <summary>
    /// What the widget is for. The parameter is written through rather than escaped, which is what
    /// makes this the probe day-one check 3 needs.
    /// </summary>
    [HardenedTest]
    public async Task TheEchoParameterIsWrittenThrough(LambdaInvocationHandler handler) {
        var shown = await Shown(handler,
            """{"widgetContext":{"params":{"echo":"<h1>Hello world</h1>"}}}""");

        Assert.Contains("<h1>Hello world</h1>", shown.Content);
    }

    /// <summary>
    /// A widget with no configured parameters still renders, because a dashboard author adds one
    /// before configuring it and an empty panel reads as a broken widget.
    /// </summary>
    [HardenedTest]
    public async Task AWidgetWithNoParametersStillRenders(LambdaInvocationHandler handler) {
        Assert.Contains("Hello world", (await Shown(handler, """{"widgetContext":{}}""")).Content);
    }

    /// <summary>
    /// The event the console sent, shown on the dashboard. A widget author's first question is what
    /// the console actually sends, and this is the sample that answers it.
    /// </summary>
    [HardenedTest]
    public async Task TheWidgetShowsTheContextItWasInvokedWith(LambdaInvocationHandler handler) {
        var shown = await Shown(handler, """
            {"widgetContext":{"dashboardName":"ops","widgetId":"widget-16","accountId":"012345678901",
                              "theme":"dark","width":588,"height":369}}
            """);

        Assert.Contains("ops", shown.Content);
        Assert.Contains("widget-16", shown.Content);
        Assert.Contains("012345678901", shown.Content);
        Assert.Contains("588 x 369", shown.Content);
    }

    /// <summary>
    /// The answer is HTML the console can render, which is the property the whole response path
    /// exists to produce.
    /// </summary>
    [HardenedTest]
    public async Task TheAnswerIsHtmlTheConsoleRenders(LambdaInvocationHandler handler) {
        Assert.Equal(ResponseKind.Html, (await Shown(handler, """{"widgetContext":{}}""")).Kind);
    }

    /// <summary>
    /// Nothing the sample emits is stripped. A sample whose own markup the console removes teaches
    /// the wrong thing to whoever copies it.
    /// </summary>
    [HardenedTest]
    public async Task NothingTheSampleEmitsIsStripped(LambdaInvocationHandler handler) {
        var shown = await Shown(handler, """{"widgetContext":{"params":{"echo":"<p>ok</p>"}}}""");

        Assert.Empty(Sanitizer.Clean(shown.Content).Removals);
    }

    // ------------------------------------------------------------------ describe

    /// <summary>
    /// The console's <em>Get documentation</em> button. AWS strongly recommends answering it, and a
    /// function that answers with a render instead shows a dashboard author its widget where its
    /// documentation should be.
    /// </summary>
    [HardenedTest]
    public async Task DescribeAnswersWithDocumentationRatherThanTheWidget(LambdaInvocationHandler handler) {
        var shown = await Shown(handler, """{"describe":true,"widgetContext":{}}""");

        Assert.Equal(ResponseKind.Markdown, shown.Kind);
        Assert.Contains("## Echo", shown.Content);
    }

    /// <summary>
    /// The console lifts the first fenced yaml block into the widget's parameters editor, which is
    /// how a dashboard author learns what a widget takes. Read back with the interpreter, because
    /// that is where the console's behaviour is modelled.
    /// </summary>
    [HardenedTest]
    public async Task TheDocumentationOffersItsParameters(LambdaInvocationHandler handler) {
        var shown = await Shown(handler, """{"describe":true,"widgetContext":{}}""");

        Assert.Equal(
            "echo: <h1>Hello world</h1>",
            new WidgetDocumentation().Parameters(shown.Content));
    }

    /// <summary>
    /// Describe short-circuits before dispatch, so the widget's own work never runs. On a search
    /// widget that would mean a describe running the search.
    ///
    /// <para>
    /// The echoed value is one the documentation does not itself contain, which the first draft of
    /// this got wrong: asserting the answer omits <c>Hello world</c> fails, because the yaml block
    /// the console lifts uses exactly that as its example.
    /// </para>
    /// </summary>
    [HardenedTest]
    public async Task DescribeDoesNotRunTheWidget(LambdaInvocationHandler handler) {
        var shown = await Shown(handler,
            """{"describe":true,"widgetContext":{"params":{"echo":"<b>rendered</b>"}}}""");

        Assert.DoesNotContain("<b>rendered</b>", shown.Content);
    }

    private static async Task<WidgetResponse> Shown(
        LambdaInvocationHandler handler, string payload) {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var output = await handler.Invoke(input, new EchoContext());

        return Responses.Classify(Encoding.UTF8.GetString(((MemoryStream)output).ToArray()));
    }

    private sealed class EchoContext : ILambdaContext {
        public string AwsRequestId => "echo-1";
        public IClientContext ClientContext => null!;
        public string FunctionName => "customWidgetEcho";
        public string FunctionVersion => "$LATEST";
        public ICognitoIdentity Identity => null!;
        public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:012345678901:function:customWidgetEcho";
        public ILambdaLogger Logger => null!;
        public string LogGroupName => "/aws/lambda/customWidgetEcho";
        public string LogStreamName => "echo";
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
    }
}
