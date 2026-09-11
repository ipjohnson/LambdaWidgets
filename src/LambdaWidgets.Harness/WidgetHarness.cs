using System.Collections.Concurrent;
using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Harness.Invoke;

namespace LambdaWidgets.Harness;

/// <summary>
/// What is on screen for one widget, and what produced it.
/// </summary>
/// <param name="Shown">The widget as the console would render it.</param>
/// <param name="Event">The JSON that was sent, which the inspector shows.</param>
/// <param name="Invoke">What came back, including how long it took and whether the function failed.</param>
/// <remarks>
/// The page's and the driver's shape. <c>WidgetView</c> in <see cref="HarnessApi"/> is the HTTP
/// API's, which is JSON and carries none of the types above.
/// </remarks>
public sealed record WidgetRender(ShownWidget Shown, string Event, InvokeResult Invoke);

/// <summary>
/// The console, locally: a dashboard of widgets, the state they are invoked with, and what they
/// last answered.
/// </summary>
/// <remarks>
/// Every rule about widgets is <see cref="IWidgetConsole"/>'s. This holds the state a dashboard has
/// and nothing else, which is what keeps the harness and the test driver from disagreeing.
/// </remarks>
public interface IWidgetHarness {
    DashboardBody Dashboard { get; }

    /// <summary>The dashboard's own controls: time range, theme, period, account.</summary>
    DashboardState State { get; set; }

    /// <summary>What each widget last rendered, for the page and the inspector.</summary>
    IReadOnlyDictionary<string, WidgetRender> Shown { get; }

    /// <summary>Invokes a widget as the console does on load.</summary>
    Task<WidgetRender> Open(string widgetId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-invokes every widget whose <c>updateOn</c> says this trigger should.
    /// </summary>
    Task RefreshAll(DashboardTrigger trigger, CancellationToken cancellationToken);

    /// <summary>Fires one of a shown widget's actions.</summary>
    Task<WidgetRender> Click(
        string widgetId,
        int action,
        IReadOnlyDictionary<string, string> edits,
        CancellationToken cancellationToken);

    /// <summary>Asks a widget for its documentation, as the console's button does.</summary>
    Task<WidgetRender> Describe(string widgetId, CancellationToken cancellationToken);
}

/// <summary>What the viewer did to the dashboard, which decides which widgets re-invoke.</summary>
public enum DashboardTrigger {
    Refresh,
    Resize,
    TimeRange
}

/// <inheritdoc />
[SingletonService(As = typeof(IWidgetHarness))]
public sealed class WidgetHarness : IWidgetHarness {
    private readonly IWidgetConsole _console;
    private readonly IWidgetInvoker _invoker;
    private readonly ConcurrentDictionary<string, WidgetRender> _shown = new(StringComparer.Ordinal);

    public WidgetHarness(IWidgetConsole console, IWidgetInvoker invoker, DashboardBody dashboard) {
        _console = console;
        _invoker = invoker;
        Dashboard = dashboard;
    }

    public DashboardBody Dashboard { get; }

    public DashboardState State { get; set; } = new();

    public IReadOnlyDictionary<string, WidgetRender> Shown => _shown;

    public Task<WidgetRender> Open(string widgetId, CancellationToken cancellationToken) =>
        Send(Widget(widgetId), _console.Opens(Widget(widgetId), State), cancellationToken);

    /// <remarks>
    /// <b>Per widget, and with the configured parameters.</b> A widget the viewer navigated three
    /// pages into returns to its landing page, because that is what the console does — it keeps no
    /// state between invocations. Reproducing it here rather than smoothing it over is the point of
    /// the harness: a widget has to be designed for it, and an author who only ever sees it in
    /// production has already shipped.
    /// </remarks>
    public async Task RefreshAll(DashboardTrigger trigger, CancellationToken cancellationToken) {
        foreach (var widget in Dashboard.Widgets) {
            if (!Wants(widget, trigger)) {
                continue;
            }

            await Send(widget, _console.Refreshes(widget, State), cancellationToken);
        }
    }

    public Task<WidgetRender> Click(
        string widgetId,
        int action,
        IReadOnlyDictionary<string, string> edits,
        CancellationToken cancellationToken) {
        var widget = Widget(widgetId);

        if (!_shown.TryGetValue(widgetId, out var view)) {
            throw new InvalidOperationException(
                $"Widget '{widgetId}' has not been rendered, so it has no action to fire. Open it " +
                "first.");
        }

        return Send(widget, _console.Clicks(view.Shown, action, edits, widget, State), cancellationToken);
    }

    public Task<WidgetRender> Describe(string widgetId, CancellationToken cancellationToken) =>
        Send(Widget(widgetId), _console.AsksForDocumentation(Widget(widgetId), State), cancellationToken);

    /// <remarks>
    /// The function name comes from the widget's own endpoint rather than from a setting, so one
    /// dashboard can hold widgets served by different targets at once.
    /// </remarks>
    private async Task<WidgetRender> Send(
        DashboardWidget widget, WidgetEvent widgetEvent, CancellationToken cancellationToken) {
        var payload = widgetEvent.ToJson();

        var result = await _invoker.Invoke(FunctionName(widget), payload, cancellationToken);

        var view = new WidgetRender(_console.Shows(result.Body, State), payload, result);

        _shown[widget.Id] = view;

        return view;
    }

    private static bool Wants(DashboardWidget widget, DashboardTrigger trigger) => trigger switch {
        DashboardTrigger.Refresh => widget.UpdateOn.Refresh,
        DashboardTrigger.Resize => widget.UpdateOn.Resize,
        DashboardTrigger.TimeRange => widget.UpdateOn.TimeRange,
        _ => true
    };

    /// <remarks>
    /// <c>KeyNotFoundException</c> rather than <c>InvalidOperationException</c>, matching
    /// <see cref="HarnessRegistry.Get"/>: both are a lookup that missed, and
    /// <see cref="HarnessErrors"/> is what turns one into a 404 carrying this message rather than
    /// a 500 carrying none.
    /// </remarks>
    private DashboardWidget Widget(string widgetId) =>
        Dashboard.Widgets.FirstOrDefault(one => one.Id == widgetId)
        ?? throw new KeyNotFoundException(
            $"This dashboard has no widget '{widgetId}'. It has: " +
            string.Join(", ", Dashboard.Widgets.Select(one => one.Id)) + ".");

    /// <summary>The segment after <c>function:</c>, which is what selects an invoke target.</summary>
    private static string FunctionName(DashboardWidget widget) {
        var marker = widget.Endpoint.LastIndexOf("function:", StringComparison.Ordinal);

        if (marker < 0) {
            return widget.Endpoint;
        }

        var name = widget.Endpoint[(marker + "function:".Length)..];
        var qualifier = name.IndexOf(':');

        return qualifier < 0 ? name : name[..qualifier];
    }
}
