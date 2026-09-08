using Hardened.Requests.Abstract.Attributes;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Charts;
using LambdaWidgets.Runtime;

namespace Graph;

/// <summary>
/// Charting a data source, as a widget.
/// </summary>
/// <remarks>
/// The sample stands in for a query rather than running one, so it can be opened with no AWS
/// account. Where <see cref="Metrics"/> makes numbers up, a real widget calls Logs Insights,
/// DynamoDB or CloudWatch and shapes the answer into the same <c>Series</c>.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[ChartsModule]
[Enable<ChartTemplates>]
public partial class GraphApp { }

public class Pages(Metrics metrics) {

    /// <summary>A line over the dashboard's range, a breakdown you can click, and a headline.</summary>
    [Get("/")]
    [Output<Views.GraphPage>]
    public GraphPage Index(IWidgetContext context) {
        var (start, end) = context.TimeRange.Effective;

        return new GraphPage(
            // Three series within an order of magnitude of each other. Two orders apart and the
            // smaller ones flatten onto the baseline - which is the honest answer, because a second
            // y-axis would invent a correlation that is not in the data. Where the scales really do
            // differ that much, draw two charts.
            Requests: Chart.Over("Errors by kind",
                    metrics.Over(start, end, "timeout", 180, 0.4),
                    metrics.Over(start, end, "throttled", 90, 0.7),
                    metrics.Over(start, end, "5xx", 30, 1.1))
                .In("per 5 min"),

            // Clicking a column drills in, which is the thing worth demonstrating: a chart is not
            // a picture here, it is a set of links.
            ByFunction: Chart.Across("Errors by function", metrics.ByFunction())
                .In("in range")
                .DrillingTo("/function", "function"),

            Open: Chart.Number("Open incidents", 3),
            Detail: null);
    }

    /// <summary>Where a click on a column lands.</summary>
    [Get("/function")]
    [Output<Views.GraphPage>]
    public GraphPage Function([FromWidget] DrillRequest request, IWidgetContext context) {
        var (start, end) = context.TimeRange.Effective;

        return new GraphPage(
            Requests: Chart.Over($"Errors in {request.Function}",
                    metrics.Over(start, end, request.Function, 40, 0.9))
                .In("per 5 min"),
            ByFunction: null,
            Open: null,
            Detail: request.Function);
    }
}

public class DrillRequest {
    public string Function { get; set; } = "";
}

public record GraphPage(Chart? Requests, Chart? ByFunction, Chart? Open, string? Detail);
