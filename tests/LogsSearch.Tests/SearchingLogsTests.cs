using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace LogsSearch.Tests;

/// <summary>
/// The search widget, driven the way a viewer drives it.
/// </summary>
public class SearchingLogsTests {

    // ------------------------------------------------------------------ the form

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
    public async Task AnUnconfiguredWidgetOffersADefaultQuery(IWidgetDriver widget) {
        Assert.Contains("fields @timestamp", (await widget.Open()).Forms["query"]);
    }

    // ------------------------------------------------------------------ running a search

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
    /// searches the window the viewer is looking at. A time field on the form would be a second
    /// place for it to be wrong.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsRangeIsWhatIsSearched(
        IWidgetDriver widget, StandInLogQueries logs) {
        var end = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

        widget.State = widget.State with { TimeRange = WidgetTimeRange.Relative(TimeSpan.FromHours(3), end) };

        await widget.Open();
        await widget.Click("Run query");

        Assert.Equal(DateTimeOffset.Parse("2026-09-08T09:00:00Z"), logs.Asked!.Value.Start);
        Assert.Equal(end, logs.Asked!.Value.End);
    }

    /// <summary>
    /// And it follows a zoom, which is the behaviour a viewer expects of the widget next to the
    /// graph they just zoomed.
    /// </summary>
    [HardenedTest]
    public async Task AZoomedGraphNarrowsTheSearch(IWidgetDriver widget, StandInLogQueries logs) {
        var end = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

        widget.State = widget.State with {
            TimeRange = WidgetTimeRange.Relative(TimeSpan.FromHours(3), end)
                .ZoomedTo(DateTimeOffset.Parse("2026-09-08T11:00:00Z"), end)
        };

        await widget.Open();
        await widget.Click("Run query");

        Assert.Equal(DateTimeOffset.Parse("2026-09-08T11:00:00Z"), logs.Asked!.Value.Start);
    }

    // ------------------------------------------------------------------ the results

    [HardenedTest]
    public async Task TheRowsAreShownAsATable(IWidgetDriver widget) {
        await widget.Open();

        var results = await widget.Click("Run query");

        Assert.Contains("order placed", results.Html);
        Assert.Contains("@timestamp", results.Html);
        Assert.Contains("12345 record(s) scanned", results.Html.Replace(",", ""));
    }

    /// <summary>
    /// A message wide enough to matter does not fit a widget, and the console gives no other way to
    /// read one.
    /// </summary>
    [HardenedTest]
    public async Task EachRowOpensInFull(IWidgetDriver widget) {
        await widget.Open();

        var results = await widget.Click("Run query");

        Assert.Contains(results.Actions, action => action.Kind == ActionKind.Html);
    }

    /// <summary>
    /// A query that matched nothing is a normal answer, and saying which status produced it is what
    /// tells a viewer whether to widen the range or fix the query.
    /// </summary>
    [HardenedTest]
    public async Task AQueryThatMatchedNothingSaysSo(IWidgetDriver widget, StandInLogQueries logs) {
        logs.Answer = new LogResults([], Scanned: 0, Status: "Complete");

        await widget.Open();

        Assert.Contains("No rows", (await widget.Click("Run query")).Html);
    }

    /// <summary>The form survives a search, so a viewer can edit and run again.</summary>
    [HardenedTest]
    public async Task TheQueryIsStillEditableAfterASearch(IWidgetDriver widget) {
        await widget.Open();
        widget.Fill("query", "stats count(*)");

        var results = await widget.Click("Run query");

        Assert.Equal("stats count(*)", results.Forms["query"]);
    }

    // ------------------------------------------------------------------ resetting

    /// <summary>
    /// Reset asks first, because discarding a query someone was working on is the kind of loss a
    /// confirmation exists for.
    /// </summary>
    [HardenedTest]
    public async Task ResetPutsTheConfiguredQueryBack(IWidgetDriver widget) {
        widget.Params["query"] = "the configured one";

        await widget.Open();
        widget.Fill("query", "the edited one");

        Assert.Equal("the configured one", (await widget.Click("Reset")).Forms["query"]);
    }

    [HardenedTest]
    public async Task DecliningTheResetKeepsTheEditedQuery(IWidgetDriver widget) {
        var before = await widget.Open();

        Assert.Same(before, widget.Decline("Reset"));
    }

    // ------------------------------------------------------------------ what the console sees

    [HardenedTest]
    public async Task NothingTheWidgetEmitsIsFaultyOrStripped(IWidgetDriver widget) {
        await widget.Open();

        var results = await widget.Click("Run query");

        Assert.Empty(results.Removals);
        Assert.Empty(results.Actions.SelectMany(action => action.Faults));
    }

    [HardenedTest]
    public async Task TheDocumentationOffersItsParameters(IWidgetDriver widget) {
        var parameters = new WidgetDocumentation().Parameters((await widget.Describe()).Content);

        Assert.Contains("logGroups:", parameters);
        Assert.Contains("limit:", parameters);
    }
}
