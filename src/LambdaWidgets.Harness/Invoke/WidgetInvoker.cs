using System.Net.Http.Headers;
using System.Text;
using DependencyModules.Runtime.Attributes;

namespace LambdaWidgets.Harness.Invoke;

/// <summary>What came back from an invoke.</summary>
/// <param name="Body">The function's result, as the Invoke API returned it.</param>
/// <param name="FunctionError">
/// Set when the function failed. The console shows a viewer nothing useful in this case, so the
/// harness shows the payload — which is most of why developing against the harness beats developing
/// against a dashboard.
/// </param>
/// <param name="Duration">How long the invoke took, for the inspector.</param>
public sealed record InvokeResult(string Body, string? FunctionError, TimeSpan Duration) {
    public bool Failed => FunctionError is not null;
}

/// <summary>Sends a widget's event to whatever is serving it.</summary>
public interface IWidgetInvoker {
    Task<InvokeResult> Invoke(string functionName, string payload, CancellationToken cancellationToken);
}

/// <summary>
/// The Invoke API over HTTP, which every emulator implements and the deployed service does too.
/// </summary>
/// <remarks>
/// <para>
/// <b>Server side, always.</b> The page never calls a target itself, for two reasons that are both
/// enough on their own: the test tool's Invoke API sets no CORS headers, so a browser cannot reach
/// it; and credentials for a deployed function must not be in a browser.
/// </para>
/// <para>
/// A function that threw answers 200 with <c>X-Amz-Function-Error</c> set and the exception in the
/// body, which is the Invoke API's contract and not an HTTP failure. Reading the status alone would
/// report every crashed widget as a success.
/// </para>
/// </remarks>
[SingletonService(As = typeof(IWidgetInvoker))]
public sealed class WidgetInvoker : IWidgetInvoker {
    /// <summary>The header the Lambda service sets when the function itself failed.</summary>
    public const string FunctionErrorHeader = "X-Amz-Function-Error";

    /// <summary>
    /// Long, because a widget may legitimately take that long.
    /// </summary>
    /// <remarks>
    /// A Logs Insights query is started and polled inside the invocation, so a search widget's first
    /// call is seconds rather than milliseconds. A timeout tuned for a fast handler would report
    /// the slowest and most interesting widget as broken.
    /// </remarks>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient _client;
    private readonly InvokeRouting _routing;

    public WidgetInvoker(HttpClient client, InvokeRouting routing) {
        _client = client;
        _routing = routing;
    }

    public async Task<InvokeResult> Invoke(
        string functionName, string payload, CancellationToken cancellationToken) {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, _routing.For(functionName).UriFor(functionName)) {
            Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        deadline.CancelAfter(Timeout);

        try {
            using var response = await _client.SendAsync(request, deadline.Token);

            var body = await response.Content.ReadAsStringAsync(deadline.Token);

            return new InvokeResult(
                body,
                response.Headers.TryGetValues(FunctionErrorHeader, out var error)
                    ? error.FirstOrDefault() ?? "Unhandled"
                    : null,
                System.Diagnostics.Stopwatch.GetElapsedTime(started));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException) {
            // The target is not listening, or took too long. Reported as a result rather than
            // thrown, because the answer a viewer needs is which target was tried and why it did
            // not answer - a stack trace from the harness says neither.
            return new InvokeResult(
                $"The harness could not reach {_routing.For(functionName).UriFor(functionName)}. " +
                $"It is configured for {_routing.For(functionName).Description}. " +
                (failure is TaskCanceledException
                    ? $"The invoke did not answer within {Timeout.TotalSeconds:0} seconds."
                    : failure.Message),
                "HarnessCouldNotReachTarget",
                System.Diagnostics.Stopwatch.GetElapsedTime(started));
        }
    }
}
