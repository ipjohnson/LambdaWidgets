using System.Net;
using Hardened.Shared.Runtime.Application;

namespace LambdaWidgets.Runtime;

/// <summary>
/// The page a viewer sees when an invocation failed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page rather than an object.</b> The pipeline's answer to a thrown handler is an
/// <c>ErrorModel</c>, and the console renders an object it does not recognise as JSON — so a widget
/// whose query was throttled or whose role was denied shows a viewer
/// <c>{"type":"ServerError",...}</c> where a designed page should be. This is what the adapter
/// writes instead.
/// </para>
/// <para>
/// Registered with <c>TryAdd</c>, so a widget with a house style writes its own.
/// </para>
/// </remarks>
public interface IWidgetErrors {
    /// <summary>Whether the exception's own message is written into the page.</summary>
    bool ShowsDetail { get; }

    /// <summary>The markup the console renders in place of the widget.</summary>
    /// <param name="failure">What the handler threw.</param>
    /// <param name="requestId">The invocation's request id, which joins this page to the log.</param>
    string Page(Exception failure, string requestId);
}

/// <inheritdoc />
public sealed class WidgetErrors : IWidgetErrors {
    /// <summary>
    /// The attribute that marks a page as this one rather than a widget's own.
    /// </summary>
    /// <remarks>
    /// <b>It is the only thing the runtime and the console interpreter agree on out of band.</b>
    /// The console reads one thing, the Invoke response body, so a failed invocation and a
    /// successful one are the same shape by the time <c>LambdaWidgets.Dashboard</c> sees them. The
    /// interpreter reads this attribute to tell them apart, which is what makes
    /// <c>Assert.Empty(page.Findings)</c> fail on a widget that threw. The console ignores it.
    /// </remarks>
    public const string Marker = "data-lw-error";

    /// <summary>The variable that turns the exception's own message on.</summary>
    /// <remarks>
    /// Off by default, because this page reaches a real dashboard and an exception message is the
    /// function author's rather than the viewer's. Set it in the function's configuration while
    /// developing: the alternative is reading the test tool's output for a message the page could
    /// have shown.
    /// </remarks>
    public const string DetailVariable = "WIDGET_ERROR_DETAIL";

    public WidgetErrors(IHardenedEnvironment? environment = null) =>
        ShowsDetail = On(environment?.Value<string>(DetailVariable) ??
                         Environment.GetEnvironmentVariable(DetailVariable));

    public bool ShowsDetail { get; }

    /// <remarks>
    /// Inline styles rather than a <c>style</c> block, because a block here would be a rule the
    /// linter reports as unscoped on every widget that ever failed.
    /// </remarks>
    public string Page(Exception failure, string requestId) {
        var page =
            $"""
             <div {Marker}="{Escape(failure.GetType().Name)}" style="border-left:3px solid #d13212;padding-left:10px">
               <p style="margin:0 0 4px"><b>This widget could not be rendered.</b></p>
             """;

        if (ShowsDetail) {
            page += $"""

                       <pre style="margin:0 0 4px;white-space:pre-wrap;font-size:12px">{Escape(failure.Message)}</pre>
                     """;
        }

        return page +
               $"""

                  <p style="margin:0;font-size:12px;color:#687078">Request {Escape(requestId)}</p>
                </div>
                """;
    }

    private static bool On(string? value) =>
        value is not null &&
        (value.Equals("1", StringComparison.Ordinal) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("on", StringComparison.OrdinalIgnoreCase));

    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
