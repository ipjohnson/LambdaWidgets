using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace WidgetName.Tests;

/// <summary>
/// The widget, driven the way a viewer drives it.
/// </summary>
/// <remarks>
/// Every test here says what the widget should do, never how it does it. It opens the widget, types
/// into a field by its name, clicks by the text on screen, and asserts on what came back - which is
/// all a viewer can do, and all that has to keep working when the inside is rewritten.
/// </remarks>
public class SearchingTests {

    /// <summary>
    /// A dashboard author configures the widget and a viewer edits it, which is the division the
    /// console's parameters exist for: the widget arrives useful and stays editable.
    /// </summary>
    [HardenedTest]
    public async Task TheFormArrivesFilledFromTheDashboard(IWidgetDriver widget) {
        widget.Params["logGroups"] = "/aws/lambda/orders";
        widget.Params["query"] = "fields @timestamp | limit 5";

        var shown = await widget.Open();

        Assert.Equal("/aws/lambda/orders", shown.Forms["logGroups"]);
        Assert.Equal("fields @timestamp | limit 5", shown.Forms["query"]);
    }

    /// <summary>A widget nobody has configured still offers a query worth running.</summary>
    [HardenedTest]
    public async Task AnUnconfiguredWidgetOffersADefaultQuery(IWidgetDriver widget) =>
        Assert.Contains("fields @timestamp", (await widget.Open()).Forms["query"]);

    [HardenedTest]
    public async Task WhatTheViewerTypedIsWhatIsSearched(
        IWidgetDriver widget, StandInLogQueries logs) {
        widget.Params["logGroups"] = "/aws/lambda/orders";

        await widget.Open();
        widget.Fill("query", "stats count(*) by bin(5m)");

        await widget.Click("Run query");

        Assert.Equal("stats count(*) by bin(5m)", logs.Asked!.Value.Query);
        Assert.Equal("/aws/lambda/orders", logs.Asked!.Value.LogGroups);
    }

    /// <summary>
    /// The range comes from the dashboard rather than from a field, so a widget beside a graph
    /// searches the window the viewer is looking at.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsRangeIsWhatIsSearched(
        IWidgetDriver widget, StandInLogQueries logs) {
        var start = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var end = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

        widget.State = widget.State with { TimeRange = WidgetTimeRange.Absolute(start, end) };

        await widget.Open();
        await widget.Click("Run query");

        Assert.Equal(start, logs.Asked!.Value.Start);
        Assert.Equal(end, logs.Asked!.Value.End);
    }

    [HardenedTest]
    public async Task TheRowsThatCameBackAreWhatIsShown(IWidgetDriver widget) {
        await widget.Open();

        Assert.Contains("order placed", (await widget.Click("Run query")).Html);
    }

    /// <summary>
    /// Declining a confirmation does nothing at all - the property a confirmation exists to
    /// provide, and one nobody checks until it has failed.
    /// </summary>
    [HardenedTest]
    public async Task DecliningTheResetKeepsWhatTheViewerTyped(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("query", "fields @message");

        Assert.Equal(widget.Shown.Html, widget.Decline("Reset").Html);
    }

    /// <summary>
    /// A refresh puts the widget back on its landing page. The console keeps no state between
    /// invocations, and a widget that pretended otherwise would surprise its author.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshReturnsTheWidgetToItsLandingPage(IWidgetDriver widget) {
        await widget.Open();
        await widget.Click("Run query");

        Assert.DoesNotContain("Back", (await widget.Refresh()).Actions.Select(one => one.Text));
    }

    /// <summary>
    /// Nothing the widget renders is something the console would strip. A widget whose action does
    /// not bind renders perfectly and then does nothing, which is the worst failure it has.
    /// </summary>
    [HardenedTest]
    public async Task NothingTheWidgetRendersIsThrownAway(IWidgetDriver widget) =>
        Assert.Empty((await widget.Open()).Findings);
}
