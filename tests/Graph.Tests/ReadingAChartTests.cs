using System.Xml.Linq;
using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Testing;
using Xunit;

namespace Graph.Tests;

/// <summary>
/// What a viewer can get out of a chart, driven the way they drive one.
/// </summary>
public class ReadingAChartTests {

    // ------------------------------------------------------------------ the hover layer

    /// <summary>
    /// The thing a chart with several lines is for: what every series was at one moment, without
    /// having to land the pointer on a two-pixel line.
    /// </summary>
    [HardenedTest]
    public async Task PointingAtAMomentShowsEverySeriesAtThatMoment(IWidgetDriver widget) {
        var line = Drawing.Titled((await widget.Open()).Html, "Errors by kind");

        Assert.NotEmpty(line.Bands);

        foreach (var band in line.Bands) {
            var says = Drawing.Says(band);

            // The moment, then a value for each of the three series, then their names.
            Assert.Equal(7, says.Count);
            Assert.Equal(["timeout", "throttled", "5xx"], says.Skip(1).Where((_, i) => i % 2 == 1));
        }
    }

    /// <summary>
    /// Nothing is revealed until something is pointed at.
    /// </summary>
    /// <remarks>
    /// Hidden by an attribute and revealed by the stylesheet, never the other way round. A console
    /// that dropped the <c>style</c> block would then show a plain chart; a rule that did the
    /// hiding would leave every readout on screen at once.
    /// </remarks>
    [HardenedTest]
    public async Task NothingIsRevealedUntilThePointerArrives(IWidgetDriver widget) {
        foreach (var drawing in Drawing.In((await widget.Open()).Html)) {
            foreach (var band in drawing.Bands) {
                Assert.Equal("0", (string?)Drawing.Readout(band).Attribute("opacity"));
            }
        }
    }

    /// <summary>
    /// A keyboard reaches what the pointer reaches.
    /// </summary>
    /// <remarks>
    /// Asserted on the markup rather than on behaviour, because nothing here runs a browser: every
    /// band is focusable and the rule that reveals a readout fires on focus as well as on hover.
    /// </remarks>
    [HardenedTest]
    public async Task EveryBandIsReachableWithoutAPointer(IWidgetDriver widget) {
        var shown = await widget.Open();

        Assert.Contains(":focus-within", shown.Html);

        foreach (var drawing in Drawing.In(shown.Html)) {
            foreach (var band in drawing.Bands) {
                Assert.Contains(band.Elements(), one => one.Attribute("tabindex") is not null);
            }
        }
    }

    // ------------------------------------------------------------------ nothing is hover-only

    /// <summary>
    /// Every point the hover layer can reach is also in the table, so the numbers are readable
    /// without a pointer at all.
    /// </summary>
    [HardenedTest]
    public async Task EveryValueIsReadableWithoutHovering(IWidgetDriver widget) {
        var shown = await widget.Open();
        var line = Drawing.Titled(shown.Html, "Errors by kind");

        // Three series at every moment the crosshair stops on.
        var points = line.Bands.Count * 3;

        Assert.Equal(points, Rows(shown.Html, from: "Errors by kind"));
    }

    /// <summary>
    /// A name too long for the axis is spelled out where the reader can still get it.
    /// </summary>
    /// <remarks>
    /// The axis truncates, because five labels do not fit side by side at this width. That is only
    /// acceptable because pointing at the column writes the whole name out.
    /// </remarks>
    [HardenedTest]
    public async Task AColumnTooNarrowForItsNameStillSpellsItOut(IWidgetDriver widget) {
        var columns = Drawing.Titled((await widget.Open()).Html, "Errors by function");

        Assert.Contains(columns.Text, one => one.EndsWith('…'));

        Assert.Contains(
            columns.Bands.SelectMany(Drawing.Says), one => one == "customWidgetDynamoLookup");
    }

    // ------------------------------------------------------------------ a chart is a set of links

    /// <summary>
    /// Whether the console binds an action inside an <c>svg</c> is unverified, so the table under
    /// every chart carries the same links. This is the path that works either way.
    /// </summary>
    [HardenedTest]
    public async Task AChartDrillsIntoWhatWasClicked(IWidgetDriver widget) {
        await widget.Open();

        var shown = await widget.Click("customWidgetDynamoLookup");

        Assert.Contains("Errors in customWidgetDynamoLookup", shown.Html);
    }

    /// <summary>A drill-down is a page, so it can be left.</summary>
    [HardenedTest]
    public async Task ADrillDownCanBeLeft(IWidgetDriver widget) {
        await widget.Open();
        await widget.Click("checkout-api");

        Assert.Contains("Errors by function", (await widget.Click("← Back")).Html);
    }

    /// <summary>
    /// A refresh puts the widget back on its landing page, because the console keeps no state
    /// between invocations and a widget that pretended otherwise would surprise its author.
    /// </summary>
    [HardenedTest]
    public async Task ARefreshLeavesADrillDown(IWidgetDriver widget) {
        await widget.Open();
        await widget.Click("checkout-api");

        Assert.Contains("Errors by function", (await widget.Refresh()).Html);
    }

    // ------------------------------------------------------------------ the dashboard's controls

    /// <summary>
    /// The range comes from the dashboard, so a chart beside a graph covers the same window. A
    /// field on the widget would be a second place for it to be wrong.
    /// </summary>
    [HardenedTest]
    public async Task TheDashboardsRangeIsWhatIsDrawn(IWidgetDriver widget) {
        Assert.Contains("06:00", await Axis(widget, from: "06:00", to: "09:00"));
        Assert.Contains("13:00", await Axis(widget, from: "13:00", to: "16:00"));
    }

    private static async Task<IEnumerable<string>> Axis(IWidgetDriver widget, string from, string to) {
        widget.State = widget.State with {
            TimeRange = WidgetTimeRange.Absolute(
                DateTimeOffset.Parse($"2026-09-08T{from}:00Z"),
                DateTimeOffset.Parse($"2026-09-08T{to}:00Z"))
        };

        return Drawing.Titled((await widget.Open()).Html, "Errors by kind").Text;
    }

    /// <summary>
    /// A dark dashboard gets colours stepped for its surface, resolved on the server rather than
    /// left to a media query the console never evaluates.
    /// </summary>
    [HardenedTest]
    public async Task ADarkDashboardIsDrawnDark(IWidgetDriver widget) {
        widget.State = widget.State with { Theme = Theme.Dark };

        Assert.Contains("#3987e5", (await widget.Open()).Html);
    }

    private static int Rows(string html, string from) {
        var at = html.IndexOf(from, StringComparison.Ordinal);
        var end = html.IndexOf("</figure>", at, StringComparison.Ordinal);

        return html[at..end].Split("<tr").Length - 2;
    }
}
