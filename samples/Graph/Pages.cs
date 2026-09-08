using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Charts;
using LambdaWidgets.Runtime;

namespace Graph;

/// <summary>
/// The widget's pages.
/// </summary>
/// <remarks>
/// An ordinary class, found by the route table the generator builds from the <c>[Get]</c>
/// attributes. A widget can have as many of these as it has areas; the application class is the
/// one place that composes modules, and it is deliberately somewhere else.
/// </remarks>
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
            // a picture here, it is a set of links. The route comes from the generated Routes
            // rather than a literal: a handler has no Links property, and Routes is the same paths
            // as plain strings, so renaming Function breaks this line during the build.
            ByFunction: Chart.Across("Errors by function", metrics.ByFunction())
                .In("in range")
                .DrillingTo(GraphApp.Routes.Pages.Function(), "function"),

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
