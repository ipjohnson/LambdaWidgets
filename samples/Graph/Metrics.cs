using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Charts;

namespace Graph;

/// <summary>
/// Numbers to draw, so the sample opens with no AWS account.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where a real widget queries.</b> Replace this with the Logs Insights, DynamoDB or CloudWatch
/// call the widget is actually for, shape its rows into <c>Series</c> and <c>Category</c>, and
/// nothing else in the widget changes — which is the point of the shape being that small.
/// </para>
/// <para>
/// Seeded by the range rather than random, so the same window draws the same picture. A chart that
/// changes on refresh is a chart nobody can compare against the one beside it.
/// </para>
/// </remarks>
[SingletonService]
public class Metrics {
    /// <summary>A series over a range, in the five-minute bins a dashboard usually asks for.</summary>
    public Series Over(DateTimeOffset start, DateTimeOffset end, string name, double scale, double spread) {
        var bins = Math.Clamp((int)((end - start).TotalMinutes / 5), 6, 48);
        var step = (end - start) / bins;
        var seed = new Random(name.GetHashCode() ^ start.ToUnixTimeSeconds().GetHashCode());

        var readings = new List<Reading>(bins);

        for (var i = 0; i < bins; i++) {
            var shape = 1 + Math.Sin(i / 3.0) * 0.35;
            var noise = 1 + (seed.NextDouble() - 0.5) * spread;

            readings.Add(new Reading(start + step * i, Math.Round(Math.Max(0, scale * shape * noise))));
        }

        return new Series(name, readings);
    }

    /// <summary>A breakdown, the shape a <c>stats count(*) by</c> query returns.</summary>
    public IReadOnlyList<Category> ByFunction() => [
        new("customWidgetLogsSearch", 1_240),
        new("customWidgetDynamoLookup", 830),
        new("checkout-api", 412),
        new("orders-worker", 96),
        new("health-probe", 4)
    ];
}
