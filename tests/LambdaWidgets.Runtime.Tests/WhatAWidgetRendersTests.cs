using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Hardened.Aws.Lambda.Runtime.Hosting;
using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using Xunit;

namespace LambdaWidgets.Runtime.Tests;

/// <summary>
/// What a viewer gets back, and whether the console can act on it.
///
/// <para>
/// The assertions read the rendered widget through <c>LambdaWidgets.Dashboard</c> rather than by
/// matching strings, because that is the code the console's behaviour is modelled in. A test that
/// asserted on markup would agree with whoever wrote the markup; this one asserts that what the
/// helpers emit is something the interpreter can find an action in — which is the only property
/// that matters.
/// </para>
/// </summary>
public class WhatAWidgetRendersTests {
    private static readonly IWidgetActions Reader = new WidgetActions();

    /// <summary>
    /// The Invoke API returns JSON and the console renders the string it finds. A view writes raw
    /// markup into the response body, so without quoting it on the way out every template-based
    /// widget answers with something the console cannot parse — and every test reading the body
    /// directly still passes.
    /// </summary>
    [HardenedTest]
    public async Task AViewIsAnsweredAsAJsonStringTheConsoleCanRender(LambdaInvocationHandler handler) {
        var raw = await Raw(handler, """{"route":"/page","widgetContext":{}}""");

        Assert.StartsWith("\"", raw);
        Assert.Contains("<h1>Log search</h1>", JsonSerializer.Deserialize<string>(raw));
    }

    [HardenedTest]
    public async Task AHandlerReturningAStringIsAnsweredTheSameWay(LambdaInvocationHandler handler) {
        var raw = await Raw(handler, """{"widgetContext":{}}""");

        Assert.Equal("<h1>Log search</h1>", JsonSerializer.Deserialize<string>(raw));
    }

    // ------------------------------------------------------------------ what the helpers emit

    /// <summary>
    /// A widget calls back into the function and alias the viewer actually reached, read from the
    /// invocation rather than written into the template. A hard-coded ARN is what stops a widget
    /// working the moment it is deployed to a second account.
    /// </summary>
    [HardenedTest]
    public async Task ARenderedButtonCallsBackIntoTheInvokedFunction(LambdaInvocationHandler handler) {
        var action = Assert.Contains(
            "Run query", Actions(await Render(handler)).ToDictionary(one => one.Text));

        Assert.Equal(ActionKind.Call, action.Kind);
        Assert.Equal("customWidgetEcho", action.FunctionName);
    }

    /// <summary>
    /// The route rides in the action's JSON under the field the adapter reads, so a click comes
    /// back to the handler the template named.
    /// </summary>
    [HardenedTest]
    public async Task AButtonCarriesTheRouteItWasGiven(LambdaInvocationHandler handler) {
        var action = Assert.Contains(
            "Run query", Actions(await Render(handler)).ToDictionary(one => one.Text));

        Assert.Equal("/search", action.Parameters["route"]);
    }

    [HardenedTest]
    public async Task ExtraFieldsRideAlongWithTheRoute(LambdaInvocationHandler handler) {
        var action = Assert.Contains(
            "Next", Actions(await Render(handler)).ToDictionary(one => one.Text));

        Assert.Equal("/search", action.Parameters["route"]);
        Assert.Equal("c-2", action.Parameters["cursor"]);
    }

    /// <summary>
    /// The console enforces the prompt, which is the difference between a widget and the script it
    /// replaces: a confirmation there exists if its author remembered.
    /// </summary>
    [HardenedTest]
    public async Task AConfirmationIsWrittenWhereTheConsoleReadsIt(LambdaInvocationHandler handler) {
        var action = Assert.Contains(
            "Reset", Actions(await Render(handler)).ToDictionary(one => one.Text));

        Assert.Equal("Discard the edited query?", action.Confirmation);
    }

    /// <summary>An html action shows content and calls nothing.</summary>
    [HardenedTest]
    public async Task APopupShowsContentRatherThanCallingBack(LambdaInvocationHandler handler) {
        var popup = Assert.Single(Actions(await Render(handler)), one => one.Kind == ActionKind.Html);

        Assert.Equal(Dashboard.Display.Popup, popup.Display);
        Assert.Contains("It searches logs.", popup.Html);
    }

    /// <summary>Everything the helpers emit is something the console can act on.</summary>
    [HardenedTest]
    public async Task NothingTheHelpersEmitIsFaulty(LambdaInvocationHandler handler) {
        Assert.Empty(Actions(await Render(handler)).SelectMany(one => one.Faults));
    }

    // ------------------------------------------------------------------ the dashboard's theme

    /// <summary>
    /// <summary>
    /// A widget writes no wrapper of its own.
    /// </summary>
    /// <remarks>
    /// The console puts the widget's HTML inside a container it owns and puts the theme class on
    /// that - <c>cwdb-theme-dark</c>, which AWS's own samples select on as an ancestor. A widget
    /// that emitted its own container to style against would be emitting something inert in
    /// production, so the theme reaches a widget through <c>IWidgetContext</c> instead and a widget
    /// that needs different colours writes them in.
    /// </remarks>
    [HardenedTest]
    public async Task AWidgetWritesNoWrapperOfItsOwn(LambdaInvocationHandler handler) {
        var html = await Render(handler, "dark");

        Assert.DoesNotContain("cwdb-theme-dark", html);
        Assert.DoesNotContain("lw-widget", html);
    }

    /// <summary>
    /// Link text routinely holds something a widget read out of a log or a table, so it is escaped.
    /// The content of an html action is markup the author chose, so it is not.
    /// </summary>
    [HardenedTest]
    public async Task LinkTextIsEscapedAndActionContentIsNot(LambdaInvocationHandler handler) {
        var rendered = await Render(handler);

        Assert.Contains("<b>It searches logs.</b>", rendered);
        Assert.DoesNotContain("<script>", rendered);
    }

    private static IReadOnlyList<WidgetAction> Actions(string rendered) => Reader.In(rendered);

    private static async Task<string> Render(LambdaInvocationHandler handler, string theme = "dark") =>
        JsonSerializer.Deserialize<string>(
            await Raw(handler, $$$"""{"route":"/page","widgetContext":{"theme":"{{{theme}}}"}}"""))!;

    private static async Task<string> Raw(LambdaInvocationHandler handler, string payload) {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var output = await handler.Invoke(input, new EchoContext());

        return Encoding.UTF8.GetString(((MemoryStream)output).ToArray());
    }

    private sealed class EchoContext : ILambdaContext {
        public string AwsRequestId => "render-1";
        public IClientContext ClientContext => null!;
        public string FunctionName => "customWidgetEcho";
        public string FunctionVersion => "$LATEST";
        public ICognitoIdentity Identity => null!;
        public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:012345678901:function:customWidgetEcho";
        public ILambdaLogger Logger => null!;
        public string LogGroupName => "/aws/lambda/customWidgetEcho";
        public string LogStreamName => "render";
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
    }
}
