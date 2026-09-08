using LambdaWidgets.Charts;
using Xunit;

namespace Graph.Tests;

/// <summary>
/// The decisions the library makes for a widget author, which are the ones most likely to be got
/// wrong by hand.
/// </summary>
public class ChoosingAFormTests {
    private static readonly DateTimeOffset Noon = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

    /// <summary>
    /// A one-bar bar chart carries no comparison, so the number itself is the clearer answer.
    /// </summary>
    [Fact]
    public void AOneRowBreakdownIsANumberNotAOneBarChart() {
        var chart = Chart.Across("Errors", [new Category("checkout-api", 412)]);

        Assert.Equal(ChartForm.Stat, chart.Form);
    }

    [Fact]
    public void TwoRowsAreWorthComparing() =>
        Assert.Equal(
            ChartForm.Columns,
            Chart.Across("Errors", [new("checkout-api", 412), new("orders-worker", 96)]).Form);

    /// <summary>
    /// Past the palette's cap the tail is summed rather than dropped: a chart that silently omits
    /// data is worse than one that groups it, and the total still adds up.
    /// </summary>
    [Fact]
    public void PastTheCapTheTailIsSummedNotDropped() {
        var chart = Chart.Over("Errors", Enumerable.Range(1, 6)
            .Select(i => new Series($"s{i}", [new Reading(Noon, i * 10)]))
            .ToArray());

        Assert.Equal(4, chart.Series.Count);
        Assert.Equal("Other", chart.Series[^1].Name);
        Assert.Equal(210, chart.Series.Sum(one => one.Readings.Sum(reading => reading.Value)));
    }

    /// <summary>
    /// The summed tail is a fourth line and needs a fourth colour. Cycling the palette would hand
    /// it the same hue as the largest series, which is the one comparison the chart is for.
    /// </summary>
    [Fact]
    public void TheFoldedTailKeepsItsOwnColour() {
        var colours = Enumerable.Range(0, 4).Select(ChartTheme.Light.For).ToList();

        Assert.Equal(4, colours.Distinct().Count());
    }

    /// <summary>
    /// Drawing more series than the palette distinguishes is a mistake the library refuses rather
    /// than papers over, because a repeated colour looks like data.
    /// </summary>
    [Fact]
    public void MoreSeriesThanColoursIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartTheme.Light.For(4));

    /// <summary>
    /// A series with nothing in this window still holds its slot, so the lines below it keep the
    /// colour a reader learned. This is what makes the fixed argument order a real contract.
    /// </summary>
    [Fact]
    public void ASeriesWithNoDataStillHoldsItsColour() {
        var chart = Chart.Over("Errors",
            new Series("timeout", []),
            new Series("throttled", [new Reading(Noon, 5)]));

        Assert.Equal(ChartTheme.Light.For(1), ChartTheme.Light.For(chart.Series.ToList().FindIndex(
            one => one.Name == "throttled")));
    }

    /// <summary>A chart with nothing in it says so, rather than drawing an empty frame.</summary>
    [Fact]
    public void AChartWithNothingInItSaysSo() {
        var drawn = new SvgChartRenderer().Render(
            Chart.Over("Errors", new Series("timeout", [])), ChartTheme.Light, "arn:aws:lambda:x");

        Assert.Contains("nothing in this time range", drawn);
        Assert.DoesNotContain("<svg", drawn);
    }
}
