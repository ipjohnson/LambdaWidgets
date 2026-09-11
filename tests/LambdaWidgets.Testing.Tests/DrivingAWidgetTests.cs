using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using Xunit;

namespace LambdaWidgets.Testing.Tests;

/// <summary>
/// The driver, doing what a viewer does.
///
/// <para>
/// Nothing here names a route, a payload or a JSON field, because a viewer cannot name one either.
/// A test written against those breaks when the widget is rewritten without its behaviour changing,
/// which is the failure the driver exists to prevent — and the reason `Click` takes the text on the
/// button rather than an index.
/// </para>
/// </summary>
public class DrivingAWidgetTests {

    // ------------------------------------------------------------------ opening

    [HardenedTest]
    public async Task AWidgetOpensOnItsLandingPage(IWidgetDriver widget) {
        var shown = await widget.Open();

        Assert.Contains("Run query", shown.Actions.Select(action => action.Text));
        Assert.Contains("query", shown.Forms.Keys);
    }

    [HardenedTest]
    public async Task AConfiguredParameterReachesTheWidget(IWidgetDriver widget) {
        widget.Params["query"] = "stats count(*)";

        Assert.Contains("stats count(*)", (await widget.Open()).Html);
    }

    // ------------------------------------------------------------------ filling and clicking

    /// <summary>
    /// The shape a driver test takes: fill what a viewer would type, click what they would read,
    /// assert on what they would see.
    /// </summary>
    [HardenedTest]
    public async Task WhatTheViewerTypedReachesTheHandler(IWidgetDriver widget) {
        await widget.Open();

        widget.Fill("query", "fields @timestamp | limit 5");

        var results = await widget.Click("Run query");

        Assert.Contains("fields @timestamp | limit 5", results.Html);
    }

    /// <summary>A field the function rendered travels even when the viewer did not touch it.</summary>
    [HardenedTest]
    public async Task AFieldTheViewerLeftAloneStillTravels(IWidgetDriver widget) {
        await widget.Open();

        Assert.Contains("20 row(s)", (await widget.Click("Run query")).Html);
    }

    /// <summary>
    /// A field the widget does not have is the test's mistake, and the message says what it does
    /// have — because the likely cause is that the widget rendered a different page.
    /// </summary>
    [HardenedTest]
    public async Task FillingAFieldTheWidgetDoesNotHaveIsRefused(IWidgetDriver widget) {
        await widget.Open();

        var refused = Assert.Throws<InvalidOperationException>(
            () => widget.Fill("nonesuch", "x"));

        Assert.Contains("query", refused.Message);
    }

    [HardenedTest]
    public async Task ClickingSomethingThatIsNotOnScreenIsRefused(IWidgetDriver widget) {
        await widget.Open();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => widget.Click("Delete everything"));

        Assert.Contains("'Run query'", refused.Message);
    }

    // ------------------------------------------------------------------ confirmations

    /// <summary>
    /// A click accepts the confirmation, because that is what a viewer who meant it does.
    /// </summary>
    [HardenedTest]
    public async Task ClickingAConfirmedActionGoesThrough(IWidgetDriver widget) {
        await widget.Open();

        widget.Fill("query", "edited");

        Assert.Contains("fields @timestamp", (await widget.Click("Reset")).Html);
    }

    /// <summary>
    /// The property a confirmation exists to provide, and the one nobody checks until it has
    /// failed: declining really does nothing.
    /// </summary>
    [HardenedTest]
    public async Task DecliningAConfirmationChangesNothing(IWidgetDriver widget) {
        var before = await widget.Open();

        Assert.Same(before, widget.Decline("Reset"));
    }

    /// <summary>
    /// Declining something that never asked is the test's mistake, and worth refusing: a test that
    /// silently passed here would be asserting a confirmation that does not exist.
    /// </summary>
    [HardenedTest]
    public async Task DecliningSomethingThatNeverAskedIsRefused(IWidgetDriver widget) {
        await widget.Open();

        Assert.Throws<InvalidOperationException>(() => widget.Decline("Run query"));
    }

    // ------------------------------------------------------------------ refreshing

    /// <summary>
    /// The behaviour a widget author has to design for. A refresh sends the configured parameters,
    /// so a viewer two pages in is returned to the landing page — and an author who only meets it
    /// on a real dashboard has already shipped.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshReturnsTheWidgetToItsLandingPage(IWidgetDriver widget) {
        await widget.Open();

        Assert.Contains("row(s)", (await widget.Click("Run query")).Html);

        Assert.DoesNotContain("row(s)", (await widget.Refresh()).Html);
    }

    /// <summary>
    /// And what the viewer had typed is gone with it, because the console keeps no state between
    /// invocations. A driver that remembered would let a test pass on values the console discards.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshForgetsWhatTheViewerTyped(IWidgetDriver widget) {
        await widget.Open();

        widget.Fill("query", "typed but not run");

        await widget.Refresh();

        Assert.DoesNotContain("typed but not run", (await widget.Click("Run query")).Html);
    }

    // ------------------------------------------------------------------ the dashboard, and describe

    /// <summary>
    /// The theme arrives as a value the widget reads, not as a class written around it. The console
    /// puts its own theme class on the container it owns, so a widget that wants different colours
    /// resolves them here and writes them in.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsThemeReachesTheWidget(IWidgetDriver widget) {
        widget.State = widget.State with { Theme = Theme.Dark };

        Assert.Contains("Theme: Dark", (await widget.Open()).Html);
    }

    /// <summary>
    /// A widget with no documentation still answers, which is what AWS recommends and what keeps
    /// the console's button from showing an error.
    /// </summary>
    [HardenedTest]
    public async Task DescribeAnswersEvenWithNoDocumentation(IWidgetDriver widget) {
        Assert.Equal(ResponseKind.Markdown, (await widget.Describe()).Kind);
    }

    // ------------------------------------------------------------------ what the console would strip

    /// <summary>
    /// Everything this widget emits is something the console can act on. A widget whose own markup
    /// the console removes is one whose buttons do nothing on a real dashboard.
    /// </summary>
    [HardenedTest]
    public async Task NothingTheWidgetEmitsIsFaultyOrStripped(IWidgetDriver widget) {
        var shown = await widget.Open();

        Assert.Empty(shown.Removals);
        Assert.Empty(shown.Actions.SelectMany(action => action.Faults));
    }

    // ------------------------------------------------------------------ a widget that threw

    /// <summary>
    /// <c>Assert.Empty(page.Findings)</c> is the headline assertion in the README, the guide and
    /// all three templates, and this is what stops it passing on a widget that rendered nothing.
    /// A throttled query and a successful one arrive at the console as the same shape.
    /// </summary>
    [HardenedTest]
    public async Task AWidgetThatThrewIsNotAPassingTest(IWidgetDriver widget) {
        widget.Params["route"] = "/throttled";

        var shown = await widget.Open();

        Assert.True(shown.Failed);
        Assert.Equal(WidgetLinter.InvocationFailed, Assert.Single(shown.Findings).Rule);
    }

    /// <summary>And the viewer gets a page rather than a JSON object.</summary>
    [HardenedTest]
    public async Task AWidgetThatThrewShowsAPage(IWidgetDriver widget) {
        widget.Params["route"] = "/throttled";

        var shown = await widget.Open();

        Assert.Equal(ResponseKind.Html, shown.Kind);
        Assert.Contains("This widget could not be rendered.", shown.Html);
        Assert.DoesNotContain("Throughput exceeded", shown.Html);
    }
}
