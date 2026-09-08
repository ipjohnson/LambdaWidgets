using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using DependencyModules.Runtime.Attributes;

namespace LogsSearch;

/// <summary>One row a query returned, as a list of named fields.</summary>
public sealed record LogRow(IReadOnlyList<(string Field, string Value)> Fields) {
    public string Value(string field) =>
        Fields.FirstOrDefault(one => one.Field == field).Value ?? "";
}

/// <summary>What a completed query produced.</summary>
/// <param name="Rows">The rows, in the order Logs Insights returned them.</param>
/// <param name="Scanned">How many records were searched, which is what the query cost.</param>
/// <param name="Status">The query's final status, so a widget can say why it has no rows.</param>
public sealed record LogResults(IReadOnlyList<LogRow> Rows, long Scanned, string Status);

/// <summary>
/// Running a Logs Insights query and waiting for it.
/// </summary>
/// <remarks>
/// <para>
/// An interface over the two SDK calls rather than the SDK itself, so a test can drive the widget
/// without an AWS account and without asserting against a mocked <c>IAmazonCloudWatchLogs</c>. A
/// test that mocked the client would be asserting that the widget calls the SDK the way the test
/// thinks it should; what matters is what the viewer sees.
/// </para>
/// <para>
/// The polling is behind here rather than in the handler because it is the SDK's shape, not the
/// widget's: Logs Insights starts a query and you ask whether it has finished.
/// </para>
/// </remarks>
public interface ILogQueries {
    /// <summary>Runs a query over a range and waits for it, or gives up.</summary>
    Task<LogResults> Run(
        string logGroups,
        string query,
        DateTimeOffset start,
        DateTimeOffset end,
        int limit,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
/// <remarks>
/// <b>The slow call inside one invocation.</b> Logs Insights has no "run and wait" call, so this
/// starts a query and polls until it finishes — seconds, in an invocation the console is waiting
/// on. That is the shape a custom widget has to live with: there is no second round trip to come
/// back in, so the work happens now or not at all. It is also why the harness's proxy waits 60
/// seconds rather than a few.
/// </remarks>
[SingletonService(As = typeof(ILogQueries))]
public sealed class CloudWatchLogQueries(IAmazonCloudWatchLogs logs) : ILogQueries {
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(400);

    public async Task<LogResults> Run(
        string logGroups,
        string query,
        DateTimeOffset start,
        DateTimeOffset end,
        int limit,
        CancellationToken cancellationToken) {
        var started = await logs.StartQueryAsync(
            new StartQueryRequest {
                LogGroupNames = logGroups
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                QueryString = query,
                StartTime = start.ToUnixTimeSeconds(),
                EndTime = end.ToUnixTimeSeconds(),
                Limit = limit
            },
            cancellationToken);

        try {
            while (true) {
                var results = await logs.GetQueryResultsAsync(
                    new GetQueryResultsRequest { QueryId = started.QueryId }, cancellationToken);

                if (results.Status != QueryStatus.Running && results.Status != QueryStatus.Scheduled) {
                    return new LogResults(
                        results.Results.Select(Row).ToList(),
                        (long)(results.Statistics?.RecordsScanned ?? 0),
                        results.Status?.Value ?? "Unknown");
                }

                await Task.Delay(Poll, cancellationToken);
            }
        }
        catch (OperationCanceledException) {
            // The invocation is out of time. Stopping the query is not required and costs one call,
            // but a query left running keeps scanning and keeps being billed for a widget nobody is
            // looking at any more.
            await logs.StopQueryAsync(
                new StopQueryRequest { QueryId = started.QueryId }, CancellationToken.None);

            throw;
        }
    }

    /// <remarks>
    /// <c>@ptr</c> is Logs Insights' own pointer to the record and is not something a viewer reads,
    /// so it is dropped rather than rendered into a column nobody wants.
    /// </remarks>
    private static LogRow Row(List<ResultField> fields) =>
        new(fields
            .Where(field => field.Field != "@ptr")
            .Select(field => (field.Field, field.Value))
            .ToList());
}
