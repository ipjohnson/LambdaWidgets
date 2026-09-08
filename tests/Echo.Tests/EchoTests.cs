using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace Echo.Tests;

/// <summary>
/// The Echo widget, driven the way the console drives it.
///
/// <para>
/// The sample's own test and the framework's integration test at once: the adapter, the merge, the
/// typed context, the generated view base, the helpers and describe are all in the path, and none
/// of them is named below. What is asserted is what a viewer would see.
/// </para>
///
/// <para>
/// These were thirty lines of plumbing before <c>IWidgetDriver</c> existed — a hand-written
/// <c>ILambdaContext</c>, JSON payloads as string literals, and unwrapping the Invoke response by
/// hand in every test. That the plumbing is gone is item 4's whole argument.
/// </para>
/// </summary>
public class EchoTests {
    private static readonly IWidgetSanitizer Sanitizer = new WidgetSanitizer();

    // ------------------------------------------------------------------ rendering

    /// <summary>
    /// What the widget is for. The parameter is written through rather than escaped, which is what
    /// makes this the probe day-one check 3 needs.
    /// </summary>
    [HardenedTest]
    public async Task TheEchoParameterIsWrittenThrough(IWidgetDriver widget) {
        widget.Params["echo"] = "<h1>Hello world</h1>";

        Assert.Contains("<h1>Hello world</h1>", (await widget.Open()).Html);
    }

    /// <summary>
    /// A widget with no configured parameters still renders, because a dashboard author adds one
    /// before configuring it and an empty panel reads as a broken widget.
    /// </summary>
    [HardenedTest]
    public async Task AWidgetWithNoParametersStillRenders(IWidgetDriver widget) {
        Assert.Contains("Hello world", (await widget.Open()).Html);
    }

    /// <summary>
    /// The event the console sent, on the dashboard. A widget author's first question is what the
    /// console actually sends, and this is the sample that answers it.
    /// </summary>
    [HardenedTest]
    public async Task TheWidgetShowsTheContextItWasInvokedWith(IWidgetDriver widget) {
        widget.State = widget.State with { Name = "ops", AccountId = "012345678901", Theme = Theme.Dark };

        var shown = await widget.Open();

        Assert.Contains("ops", shown.Html);
        Assert.Contains("012345678901", shown.Html);
        Assert.Contains("Dark", shown.Html);
    }

    [HardenedTest]
    public async Task TheAnswerIsHtmlTheConsoleRenders(IWidgetDriver widget) {
        Assert.Equal(ResponseKind.Html, (await widget.Open()).Kind);
    }

    /// <summary>
    /// Nothing the sample emits is stripped. A sample whose own markup the console removes teaches
    /// the wrong thing to whoever copies it.
    /// </summary>
    [HardenedTest]
    public async Task NothingTheSampleEmitsIsStripped(IWidgetDriver widget) {
        widget.Params["echo"] = "<p>ok</p>";

        Assert.Empty(Sanitizer.Clean((await widget.Open()).Html).Removals);
    }

    // ------------------------------------------------------------------ describe

    /// <summary>
    /// The console's <em>Get documentation</em> button. AWS strongly recommends answering it, and a
    /// function that answers with a render shows a dashboard author its widget where its
    /// documentation should be.
    /// </summary>
    [HardenedTest]
    public async Task DescribeAnswersWithDocumentationRatherThanTheWidget(IWidgetDriver widget) {
        var documentation = await widget.Describe();

        Assert.Equal(ResponseKind.Markdown, documentation.Kind);
        Assert.Contains("## Echo", documentation.Content);
    }

    /// <summary>
    /// The console lifts the first fenced yaml block into the widget's parameters editor, which is
    /// how a dashboard author learns what a widget takes.
    /// </summary>
    [HardenedTest]
    public async Task TheDocumentationOffersItsParameters(IWidgetDriver widget) {
        Assert.Equal(
            "echo: <h1>Hello world</h1>",
            new WidgetDocumentation().Parameters((await widget.Describe()).Content));
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
    public async Task DescribeDoesNotRunTheWidget(IWidgetDriver widget) {
        widget.Params["echo"] = "<b>rendered</b>";

        Assert.DoesNotContain("<b>rendered</b>", (await widget.Describe()).Content);
    }
}
