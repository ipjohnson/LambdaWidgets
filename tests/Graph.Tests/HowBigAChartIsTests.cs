using LambdaWidgets.Charts;
using Xunit;

namespace Graph.Tests;

/// <summary>
/// A chart's coordinate space, which used to be two private constants nothing could reach.
/// </summary>
/// <remarks>
/// The drawing scales down to fit and never grows, so a fixed 640 in a 24-column widget fills under
/// half of it and leaves the rest empty.
/// </remarks>
public class HowBigAChartIsTests {
    private static readonly DateTimeOffset Noon = DateTimeOffset.Parse("2026-09-08T12:00:00Z");

    private static readonly Chart Errors = Chart.Across("Errors by function", [
        new Category("checkout-api", 412),
        new Category("orders-api", 208)
    ]);

    [Fact]
    public void ACharDrawnWithNoSizeIsTheDefault() {
        Assert.Contains($"""viewBox="0 0 {ChartSize.Default.Width} """, Drawn(Errors));
    }

    /// <summary>
    /// The widget's own width, which the event carries. <c>@Draw</c> passes it; nothing read it
    /// before.
    /// </summary>
    [Fact]
    public void AChartIsDrawnAtTheWidthItWasGiven() {
        var drawn = Drawn(Errors, ChartSize.For(1100));

        Assert.Contains("""viewBox="0 0 1100 """, drawn);
        Assert.Contains("""width="1100" """, drawn);
    }

    /// <summary>
    /// The axis moves with the canvas. A chart that took a width and drew its gridlines at 624
    /// would be worse than one that ignored the width entirely.
    /// </summary>
    [Fact]
    public void TheAxisReachesTheEdgeOfTheWidthItWasGiven() {
        var grid = Drawing.Titled(Drawn(Errors, ChartSize.For(1100)), "Errors by function");

        Assert.Contains(grid.All("line"), line => line.Attribute("x2")?.Value == "1084");
    }

    /// <summary>
    /// A view holding a heading, three charts and a table is the case this exists for: only the
    /// view knows how much of the panel each chart may have.
    /// </summary>
    [Fact]
    public void AChartThatWasGivenItsOwnSizeKeepsIt() {
        var drawn = Drawn(Errors.Sized(400, 180), ChartSize.For(1100));

        Assert.Contains("""viewBox="0 0 400 """, drawn);
    }

    /// <summary>
    /// The pixel width in the event is an estimate — the console's grid is responsive — so a chart
    /// is not drawn arbitrarily wide or unreadably narrow on the strength of it.
    /// </summary>
    [Fact]
    public void TheWidgetsWidthIsClamped() {
        Assert.Equal(ChartSize.Widest, ChartSize.For(5000).Width);
        Assert.Equal(ChartSize.Narrowest, ChartSize.For(90).Width);
        Assert.Equal(ChartSize.Default, ChartSize.For(0));
    }

    /// <summary>
    /// The height stays the design height. A widget's pixel height is what the whole panel has,
    /// and how much of it one chart may take is the view's to say.
    /// </summary>
    [Fact]
    public void TheWidgetsWidthDoesNotChangeTheHeight() {
        Assert.Equal(ChartSize.Default.Height, ChartSize.For(1100).Height);
    }

    /// <summary>A line chart takes the width too, not only the columns.</summary>
    [Fact]
    public void ALineChartIsAlsoDrawnAtTheWidthItWasGiven() {
        var chart = Chart.Over("Requests",
            new Series("ok", [new Reading(Noon, 4), new Reading(Noon.AddHours(1), 9)]));

        Assert.Contains("""viewBox="0 0 1100 """, Drawn(chart, ChartSize.For(1100)));
    }

    private static string Drawn(Chart chart, ChartSize? size = null) =>
        new SvgChartRenderer().Render(chart, ChartTheme.Light, "arn:aws:lambda:x", size);
}
