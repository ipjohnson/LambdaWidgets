using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Charts;

namespace WidgetName;

/// <summary>
/// Where the numbers come from.
/// </summary>
/// <remarks>
/// <para>
/// <b>This interface is the seam.</b> It answers <c>Series</c>, which is all the charting library
/// takes, so swapping CloudWatch for Logs Insights, DynamoDB or an internal API changes this file
/// and nothing else. A test substitutes it and drives the whole widget with no AWS account.
/// </para>
/// <para>
/// At most three series reach a chart with their own colour; past that
/// <c>Chart.Over</c> folds the tail into one summed "Other". Ask for the metrics worth comparing
/// rather than for everything.
/// </para>
/// </remarks>
public interface IMetricSource {
    Task<IReadOnlyList<Series>> Over(
        string namespaceName,
        IReadOnlyList<string> metricNames,
        DateTimeOffset start,
        DateTimeOffset end,
        int periodSeconds,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
/// <remarks>
/// One <c>GetMetricData</c> call for every metric at once, rather than one call each. The console
/// is waiting on this invocation, so the number of round trips is the widget's latency.
/// </remarks>
[SingletonService(As = typeof(IMetricSource))]
public sealed class CloudWatchMetricSource(IAmazonCloudWatch cloudWatch) : IMetricSource {

    public async Task<IReadOnlyList<Series>> Over(
        string namespaceName,
        IReadOnlyList<string> metricNames,
        DateTimeOffset start,
        DateTimeOffset end,
        int periodSeconds,
        CancellationToken cancellationToken) {
        var response = await cloudWatch.GetMetricDataAsync(
            new GetMetricDataRequest {
                StartTimeUtc = start.UtcDateTime,
                EndTimeUtc = end.UtcDateTime,
                // Oldest first, so a series is already in the order a line is drawn in.
                ScanBy = ScanBy.TimestampAscending,
                MetricDataQueries = metricNames.Select((name, i) => new MetricDataQuery {
                    // A query id has to start with a lowercase letter and is not shown to anyone.
                    Id = "m" + i,
                    Label = name,
                    MetricStat = new MetricStat {
                        Metric = new Metric { Namespace = namespaceName, MetricName = name },
                        Period = periodSeconds,
                        Stat = "Sum"
                    }
                }).ToList()
            },
            cancellationToken);

        return response.MetricDataResults
            .Select(result => new Series(
                result.Label,
                result.Timestamps
                    .Zip(result.Values, (at, value) => new Reading(at, value))
                    .OrderBy(reading => reading.At)
                    .ToList()))
            .ToList();
    }
}
