using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Hardened.Aws.Lambda.Runtime.Hosting;
using Hardened.Shared.Testing.Attributes;
using Xunit;

namespace LambdaWidgets.Runtime.Tests;

/// <summary>
/// What the console gets back, given what it sent.
///
/// <para>
/// Through <c>LambdaInvocationHandler</c>, which is the loop a deployed widget runs under: the
/// adapter is selected, the request is built, dispatch matches a route, the handler runs and the
/// adapter writes the answer. A test that called the adapter directly would prove the adapter
/// agrees with itself and say nothing about whether a widget works.
/// </para>
/// </summary>
public class WhatAWidgetInvocationDoesTests {

    // ------------------------------------------------------------------ routing

    [HardenedTest]
    public async Task AnEventWithNoRouteReachesTheLandingPage(LambdaInvocationHandler handler) {
        Assert.Equal("<h1>Log search</h1>", await Invoke(handler, """{"widgetContext":{}}"""));
    }

    /// <summary>
    /// The reserved field the framework documents for the same job on
    /// <c>InvokeAdapter.OperationField</c>: one function, many operations, selected by a field the
    /// caller sets.
    /// </summary>
    [HardenedTest]
    public async Task TheRouteFieldSelectsTheHandler(LambdaInvocationHandler handler) {
        Assert.Contains("|", await Invoke(handler, """{"route":"/search","widgetContext":{}}"""));
    }

    /// <summary>
    /// A route written without its leading slash is the author's slip rather than a different
    /// route, and repairing it beats a 404 naming a path they believe they wrote.
    /// </summary>
    [HardenedTest]
    public async Task ARouteWithoutItsLeadingSlashStillMatches(LambdaInvocationHandler handler) {
        Assert.Contains("|", await Invoke(handler, """{"route":"search","widgetContext":{}}"""));
    }

    /// <summary>
    /// Path parameters work, which is what lets a template say <c>Links.Rows.Row(id)</c> rather
    /// than build a string.
    /// </summary>
    [HardenedTest]
    public async Task ARouteWithAPathParameterBindsIt(LambdaInvocationHandler handler) {
        Assert.Equal("<p>row 42</p>",
            await Invoke(handler, """{"route":"/rows/42","widgetContext":{}}"""));
    }

    /// <summary>
    /// The round trip the generated links promise: a value goes out through <c>Routes</c>, which
    /// escapes it, and arrives at the handler as the value that was passed.
    ///
    /// <para>
    /// Every key in a single-table DynamoDB design carries a <c>#</c>, so this is the first thing
    /// that happens to anyone routing on one. Without the decode the handler binds
    /// <c>SHIPMENT%23SHP1000</c> and the lookup finds nothing, which is a widget that renders and
    /// quietly shows an empty page.
    /// </para>
    /// </summary>
    [HardenedTest]
    public async Task APathParameterArrivesAsItWasPassed(LambdaInvocationHandler handler) {
        foreach (var id in new[] { "SHIPMENT#SHP1000", "SHP 1000", "SHP/1000", "100%", "a+b" }) {
            var route = JsonSerializer.Serialize(WidgetTestApp.Routes.Pages.Row(id));

            Assert.Equal($"<p>row {id}</p>",
                await Invoke(handler, $$$"""{"route":{{{route}}},"widgetContext":{}}"""));
        }
    }

    /// <summary>
    /// An escaped separator stays inside its own segment, which is what decoding per token rather
    /// than over the whole path buys. Decoding first would split <c>%2F</c> into a third segment
    /// and the route would stop matching itself.
    /// </summary>
    [HardenedTest]
    public async Task AnEscapedSlashDoesNotBecomeASecondSegment(LambdaInvocationHandler handler) {
        Assert.Equal("<p>row a/b</p>",
            await Invoke(handler, """{"route":"/rows/a%2Fb","widgetContext":{}}"""));
    }

    // ------------------------------------------------------------------ binding

    [HardenedTest]
    public async Task AWidgetsConfiguredParametersReachTheHandler(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/search",
             "widgetContext":{"params":{"logGroups":"/aws/lambda/orders","limit":20}}}
            """);

        Assert.Equal("<p>/aws/lambda/orders||20</p>", answer);
    }

    /// <summary>
    /// What an action sent beats how the widget was configured, because the viewer's click is more
    /// recent than the dashboard author's setting.
    /// </summary>
    [HardenedTest]
    public async Task AnActionsParametersBeatTheConfiguredOnes(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/search","limit":50,
             "widgetContext":{"params":{"limit":20}}}
            """);

        Assert.Equal("<p>||50</p>", answer);
    }

    /// <summary>
    /// And what the viewer typed beats what the action sent, which is the AWS sample's
    /// <c>form.query || event.query</c> generalised. Getting this order wrong means a form that
    /// silently ignores what was typed in it.
    /// </summary>
    [HardenedTest]
    public async Task WhatTheViewerTypedBeatsEverything(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/search","query":"from the action",
             "widgetContext":{"params":{"query":"configured"},
                              "forms":{"all":{"query":"typed by the viewer"}}}}
            """);

        Assert.Equal("<p>|typed by the viewer|0</p>", answer);
    }

    // ------------------------------------------------------------------ the dashboard

    /// <summary>
    /// A handler reads where it is running from <see cref="IWidgetContext"/> rather than from its
    /// own parameters, so a tool follows the dashboard's time picker with no form field of its own.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardReachesAHandlerThroughTheContext(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/context",
             "widgetContext":{"dashboardName":"ops","widgetId":"widget-16","theme":"dark",
                              "timeRange":{"start":1627236199729,"end":1627322599729}}}
            """);

        Assert.Contains("ops|Dark|widget-16|", answer);
        Assert.Contains("2021-07-25", answer);
    }

    /// <summary>
    /// A viewer who zooms a chart expects the widget beside it to follow, so the effective range is
    /// the zoom when there is one.
    /// </summary>
    [HardenedTest]
    public async Task AZoomedChartIsTheEffectiveRange(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/context",
             "widgetContext":{"timeRange":{"start":1627236199729,"end":1627322599729,
                                           "zoom":{"start":1627276030434,"end":1627282956521}}}}
            """);

        Assert.Contains("2021-07-26", answer);
    }

    /// <summary>
    /// A payload with no <c>timeRange</c> gets the dashboard's default window rather than a
    /// zero-length one in 1970.
    ///
    /// <para>
    /// This is what the Lambda console's Test button and the test tool's default payload send. The
    /// epoch default rendered "nothing in this time range" against a full table while every driver
    /// test stayed green, because the driver sends a real range and never reached this.
    /// </para>
    /// </summary>
    [HardenedTest]
    public async Task AnEventWithNoTimeRangeGetsTheDefaultWindow(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """{"route":"/context","widgetContext":{}}""");

        var start = DateTimeOffset.Parse(answer.Split('|')[3]);

        Assert.InRange(
            DateTimeOffset.UtcNow - start,
            TimeSpan.FromHours(3) - TimeSpan.FromMinutes(1),
            TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// The ARN the helpers write into a <c>cwdb-action</c>'s endpoint, so a widget calls back into
    /// the function and alias the viewer actually reached rather than one written into a template.
    /// </summary>
    [HardenedTest]
    public async Task TheInvokedArnReachesTheContext(LambdaInvocationHandler handler) {
        Assert.Contains(
            "arn:aws:lambda:us-east-1:012345678901:function:customWidgetProbe",
            await Invoke(handler, """{"route":"/context","widgetContext":{}}"""));
    }

    // ------------------------------------------------------------------ failure

    /// <summary>
    /// Answered rather than rethrown. A rethrow gives the console a FunctionError and the viewer a
    /// blank widget; answering lets an error block reach the dashboard. The status goes nowhere,
    /// because the console reads the body.
    /// </summary>
    [HardenedTest]
    public async Task AHandlerThatThrowsStillAnswers(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """{"route":"/boom","widgetContext":{}}""");

        Assert.DoesNotContain("the widget was not ready", answer);
    }

    /// <summary>
    /// With a page, not an object. The console renders a shape it does not recognise as JSON, so a
    /// widget whose query was throttled would show an operator
    /// <c>{"type":"ServerError",...}</c> where a designed page belongs.
    /// </summary>
    [HardenedTest]
    public async Task AHandlerThatThrowsAnswersAPage(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """{"route":"/boom","widgetContext":{}}""");

        Assert.DoesNotContain("ServerError", answer);
        Assert.Contains("This widget could not be rendered.", answer);
    }

    /// <summary>
    /// The request id is on it, which is the only thing joining what a viewer saw to the function's
    /// log — and the marker is what lets a test tell this page from a widget's own.
    /// </summary>
    [HardenedTest]
    public async Task TheErrorPageCarriesTheRequestIdAndTheMarker(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """{"route":"/boom","widgetContext":{}}""");

        Assert.Contains("probe-1", answer);
        Assert.Contains($"{WidgetErrors.Marker}=\"InvalidOperationException\"", answer);
    }

    /// <summary>
    /// A request the binding could not read is answered the same way, because the console has one
    /// way of showing an answer and a JSON object is not it.
    /// </summary>
    [HardenedTest]
    public async Task ARequestTheBindingCannotReadAlsoAnswersAPage(LambdaInvocationHandler handler) {
        var answer = await Invoke(handler, """
            {"route":"/search","limit":"not a number","widgetContext":{}}
            """);

        Assert.Contains("This widget could not be rendered.", answer);
        Assert.DoesNotContain("ValidationError", answer);
    }

    /// <summary>
    /// One invocation at a time in a sandbox, but the handler is resolved once and reused across
    /// them, so a context left over from the last invocation would be served to the next.
    /// </summary>
    [HardenedTest]
    public async Task ASecondInvocationSeesItsOwnContext(LambdaInvocationHandler handler) {
        await Invoke(handler, """{"route":"/context","widgetContext":{"dashboardName":"first"}}""");

        var second = await Invoke(handler,
            """{"route":"/context","widgetContext":{"dashboardName":"second"}}""");

        Assert.StartsWith("<p>second|", second);
    }

    private static async Task<string> Invoke(LambdaInvocationHandler handler, string payload) {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var output = await handler.Invoke(input, new WidgetLambdaContext());

        // The runtime returns the adapter's stream, and what a widget wrote into it is the whole
        // answer. A handler returning a string is serialised by the IO filter on the way out, so
        // the HTML arrives quoted - which is what the console unwraps and renders.
        var body = Encoding.UTF8.GetString(((MemoryStream)output).ToArray());

        return body.StartsWith('"') ? JsonSerializer.Deserialize<string>(body)! : body;
    }

    private sealed class WidgetLambdaContext : ILambdaContext {
        public string AwsRequestId => "probe-1";
        public IClientContext ClientContext => null!;
        public string FunctionName => "customWidgetProbe";
        public string FunctionVersion => "$LATEST";
        public ICognitoIdentity Identity => null!;
        public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:012345678901:function:customWidgetProbe";
        public ILambdaLogger Logger => null!;
        public string LogGroupName => "/aws/lambda/customWidgetProbe";
        public string LogStreamName => "probe";
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
    }
}
