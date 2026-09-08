using Amazon.CloudWatch;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Requests.Abstract.Attributes;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Charts;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace WidgetName;

/// <summary>Charting a data source, as a widget.</summary>
[HardenedModule]
[LambdaWidgetModule]
[ChartsModule]
[Enable<ChartTemplates>]
public partial class WidgetNameApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAmazonCloudWatch>(_ => new AmazonCloudWatchClient());
}

public class Pages(IMetricSource metrics) {

    /// <summary>A line over the dashboard's range, and a breakdown you can click.</summary>
    /// <remarks>
    /// A handler describes charts and hands them over. Nothing here writes SVG, and nothing in the
    /// view does either.
    /// </remarks>
    [Get("/")]
    [Output<Views.GraphPage>]
    public async Task<GraphPage> Index(IWidgetContext context, CancellationToken cancellationToken) {
        var (start, end) = context.TimeRange.Effective;
        var namespaceName = Configured(context, "namespace", "AWS/Lambda");

        var series = await metrics.Over(
            namespaceName, ["Invocations", "Errors", "Throttles"], start, end,
            context.Period.Seconds, cancellationToken);

        return new GraphPage(
            // Series within an order of magnitude of each other. Two orders apart and the smaller
            // ones flatten onto the baseline - which is the honest answer, because a second y-axis
            // would invent a correlation that is not in the data. Where the scales really do differ
            // that much, draw two charts.
            Over: Chart.Over($"{namespaceName} over time", series.ToArray()).In("per period"),

            // Clicking a column drills in. A chart here is not a picture, it is a set of links.
            // The route comes from the generated Routes rather than a literal: a handler has no
            // Links property, and renaming Detail breaks this line during the build.
            Totals: Chart.Across(
                    "Total by metric",
                    series.Select(one => new Category(
                        one.Name, one.Readings.Sum(reading => reading.Value))).ToList())
                .DrillingTo(WidgetNameApp.Routes.Pages.Detail(), "metric"),

            Detail: null);
    }

    /// <summary>Where a click on a column lands.</summary>
    [Get("/detail")]
    [Output<Views.GraphPage>]
    public async Task<GraphPage> Detail(
        [FromWidget] DetailRequest request,
        IWidgetContext context,
        CancellationToken cancellationToken) {
        var (start, end) = context.TimeRange.Effective;

        var series = await metrics.Over(
            Configured(context, "namespace", "AWS/Lambda"), [request.Metric], start, end,
            context.Period.Seconds, cancellationToken);

        return new GraphPage(
            Over: Chart.Over(request.Metric, series.ToArray()).In("per period"),
            Totals: null,
            Detail: request.Metric);
    }

    private static string Configured(IWidgetContext context, string name, string fallback) =>
        context.Params.TryGetValue(name, out var value) && value.Length > 0 ? value : fallback;
}

/// <summary>Which metric a click on a column meant.</summary>
public class DetailRequest {
    public string Metric { get; set; } = "";
}

public record GraphPage(Chart? Over, Chart? Totals, string? Detail);
