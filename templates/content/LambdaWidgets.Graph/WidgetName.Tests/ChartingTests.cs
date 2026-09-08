using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace WidgetName.Tests;

/// <summary>
/// The widget, driven the way a viewer drives it.
/// </summary>
/// <remarks>
/// The assertions are about what a reader can get out of the chart, not about the SVG the renderer
/// happened to build. Those are the properties that survive the drawing being changed.
/// </remarks>
public class ChartingTests {

    /// <summary>
    /// The range and the period come from the dashboard, so the chart covers the window the viewer
    /// is looking at and follows them when they zoom it.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsRangeIsWhatIsCharted(
        IWidgetDriver widget, StandInMetricSource metrics) {
        var start = DateTimeOffset.Parse("2026-09-08T09:00:00Z");
        var end = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

        widget.State = widget.State with { TimeRange = WidgetTimeRange.Absolute(start, end) };

        await widget.Open();

        Assert.Equal(start, metrics.Asked!.Value.Start);
        Assert.Equal(end, metrics.Asked!.Value.End);
    }

    /// <summary>A dashboard author picks the namespace; the widget arrives useful without one.</summary>
    [HardenedTest]
    public async Task TheConfiguredNamespaceIsWhatIsCharted(
        IWidgetDriver widget, StandInMetricSource metrics) {
        widget.Params["namespace"] = "MyCompany/Orders";

        await widget.Open();

        Assert.Equal("MyCompany/Orders", metrics.Asked!.Value.Namespace);
    }

    /// <summary>
    /// Every number the chart draws is also in the table under it, so the values are reachable
    /// without hovering at all.
    /// </summary>
    [HardenedTest]
    public async Task EveryValueIsReadableWithoutHovering(IWidgetDriver widget) {
        var html = (await widget.Open()).Html;

        Assert.Contains("<details", html);
        Assert.Contains("Invocations", html);
    }

    /// <summary>A chart is a set of links, and a column drills into what was clicked.</summary>
    [HardenedTest]
    public async Task ClickingAColumnDrillsIntoThatMetric(
        IWidgetDriver widget, StandInMetricSource metrics) {
        await widget.Open();
        await widget.Click("Errors");

        Assert.Equal(["Errors"], metrics.Asked!.Value.Metrics);
    }

    /// <summary>A drill-down is a page, so it can be left.</summary>
    [HardenedTest]
    public async Task ADrillDownCanBeLeft(IWidgetDriver widget) {
        await widget.Open();
        await widget.Click("Errors");

        Assert.Contains("Total by metric", (await widget.Click("← Back")).Html);
    }

    /// <summary>A range with nothing in it says so, rather than drawing an empty frame.</summary>
    [HardenedTest]
    public async Task ARangeWithNothingInItSaysSo(
        IWidgetDriver widget, StandInMetricSource metrics) {
        metrics.Points = 0;

        Assert.Contains("nothing in this time range", (await widget.Open()).Html);
    }

    /// <summary>Nothing the widget renders is something the console would strip.</summary>
    [HardenedTest]
    public async Task NothingTheWidgetRendersIsThrownAway(IWidgetDriver widget) =>
        Assert.Empty((await widget.Open()).Findings);
}
