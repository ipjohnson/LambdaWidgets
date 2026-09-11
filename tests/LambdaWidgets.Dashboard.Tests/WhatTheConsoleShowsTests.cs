using Hardened.Shared.Testing.Attributes;
using System.Text.Json;
using Xunit;

namespace LambdaWidgets.Dashboard.Tests;

/// <summary>
/// What a viewer sees, given what the function returned.
/// </summary>
public class WhatTheConsoleShowsTests {
    private static readonly DashboardState Ops = new() { Theme = Theme.Light };

    [HardenedTest]
    public void AnHtmlAnswerIsRendered(IWidgetConsole console) {
        var shown = console.Shows(Answer("<h1>Orders</h1>"), Ops);

        Assert.Equal(ResponseKind.Html, shown.Kind);
        Assert.Contains("<h1>Orders</h1>", shown.Html);
    }

    /// <summary>
    /// A function answering <c>{"markdown": "..."}</c> is asking for markdown, which is a different
    /// thing from an HTML answer that happens to be an object.
    /// </summary>
    [HardenedTest]
    public void AMarkdownAnswerIsShownAsMarkdown(IWidgetConsole console) {
        var shown = console.Shows("""{"markdown":"## Orders\n\nNone today."}""", Ops);

        Assert.Equal(ResponseKind.Markdown, shown.Kind);
        Assert.Contains("## Orders", shown.Html);
    }

    /// <summary>
    /// Displayed rather than refused. A function that returned a shape the console does not
    /// recognise has a bug, and showing the author what came back is more use than an error.
    /// </summary>
    [HardenedTest]
    public void AnUnrecognisedShapeIsShownAsJson(IWidgetConsole console) {
        var shown = console.Shows("""{"orders":3,"stale":false}""", Ops);

        Assert.Equal(ResponseKind.Json, shown.Kind);
        Assert.Contains("\"orders\": 3", shown.Html);
    }

    /// <summary>
    /// Shown, and reported. The console displays an object it does not recognise, so the widget
    /// renders and carries no page — which a test asserting on the HTML alone cannot see.
    /// </summary>
    [HardenedTest]
    public void AnUnrecognisedShapeIsAlsoAFinding(IWidgetConsole console) {
        var shown = console.Shows("""{"orders":3,"stale":false}""", Ops);

        Assert.True(shown.Failed);
        Assert.Equal(WidgetLinter.NotAPage, Assert.Single(shown.Findings).Rule);
    }

    /// <summary>Markdown is a documented answer, so it is neither.</summary>
    [HardenedTest]
    public void AMarkdownAnswerIsNeitherFailedNorAFinding(IWidgetConsole console) {
        var shown = console.Shows("""{"markdown":"## Orders"}""", Ops);

        Assert.False(shown.Failed);
        Assert.Empty(shown.Findings);
    }

    /// <summary>
    /// The error page the runtime writes when a handler throws. It is HTML and it renders, so
    /// nothing about the answer's shape says the invocation failed.
    /// </summary>
    [HardenedTest]
    public void TheRuntimesErrorPageIsAFailure(IWidgetConsole console) {
        var shown = console.Shows(
            Answer("""<div data-lw-error="InvalidOperationException">This widget could not be rendered.</div>"""),
            Ops);

        Assert.True(shown.Failed);
        Assert.Equal(WidgetLinter.InvocationFailed, Assert.Single(shown.Findings).Rule);
    }

    /// <summary>And a widget that rendered is not reported as one that did not.</summary>
    [HardenedTest]
    public void AWidgetThatRenderedHasNotFailed(IWidgetConsole console) {
        Assert.False(console.Shows(Answer("<h1>Orders</h1>"), Ops).Failed);
    }

    /// <summary>
    /// Markdown and JSON are text. Parsing either as a document would invent actions and fields
    /// out of whatever happened to look like markup.
    /// </summary>
    [HardenedTest]
    public void AMarkdownAnswerHasNoActionsOrFields(IWidgetConsole console) {
        var shown = console.Shows(
            """{"markdown":"<a>x</a><cwdb-action action=\"call\" endpoint=\"e\">{}</cwdb-action>"}""",
            Ops);

        Assert.Empty(shown.Actions);
        Assert.Empty(shown.Forms);
    }

    // ------------------------------------------------------------------ what the console removes

    /// <summary>
    /// The reason for all of it is privilege: the function's author must not run code with the
    /// viewer's console permissions. The removal is also reported, because the console does it
    /// silently and an author whose button does nothing has no other way to find out.
    /// </summary>
    [HardenedTest]
    public void ScriptIsNotRenderedAndTheAuthorIsTold(IWidgetConsole console) {
        var shown = console.Shows(Answer("<p>Orders</p><script>steal()</script>"), Ops);

        Assert.DoesNotContain("steal()", shown.Html);
        Assert.Contains(shown.Removals, removal => removal.Kind == RemovalKind.Element);
    }

    [HardenedTest]
    public void AnEventHandlerAttributeIsNotRenderedAndTheAuthorIsTold(IWidgetConsole console) {
        var shown = console.Shows(Answer("""<a onclick="steal()">Go</a>"""), Ops);

        Assert.DoesNotContain("onclick", shown.Html);
        Assert.Contains(shown.Removals, removal => removal.Kind == RemovalKind.EventHandler);
    }

    /// <summary>
    /// The anchor survives with an empty href rather than being removed, so a widget that used one
    /// still renders the text the author wrote.
    /// </summary>
    [HardenedTest]
    public void AJavaScriptUrlIsEmptiedRatherThanRemoved(IWidgetConsole console) {
        var shown = console.Shows(Answer("""<a href="javascript:steal()">Go</a>"""), Ops);

        Assert.DoesNotContain("javascript:", shown.Html);
        Assert.Contains(">Go<", shown.Html);
    }

    // ------------------------------------------------------------------ default styling

    /// <summary>
    /// A bare table is styled in the console and unstyled anywhere else, which is the single
    /// largest source of "it looked right locally".
    /// </summary>
    [HardenedTest]
    public void AnHtmlAnswerCarriesTheConsolesDefaultStyles(IWidgetConsole console) {
        Assert.Contains("lw-widget table", console.Shows(Answer("<table></table>"), Ops).Styles);
    }

    [HardenedTest]
    public void TheDarkDashboardGetsDifferentStylesFromTheLightOne(IWidgetConsole console) {
        var light = console.Shows(Answer("<table></table>"), Ops).Styles;
        var dark = console.Shows(Answer("<table></table>"), Ops with { Theme = Theme.Dark }).Styles;

        Assert.NotEqual(light, dark);
    }

    /// <summary>One element carrying the class turns the defaults off for the whole widget.</summary>
    [HardenedTest]
    public void AWidgetCanTurnTheDefaultStylesOff(IWidgetConsole console) {
        var shown = console.Shows(
            Answer("""<div class="cwdb-no-default-styles"><table></table></div>"""), Ops);

        Assert.Equal("", shown.Styles);
    }

    private static string Answer(string html) => JsonSerializer.Serialize(html);
}
