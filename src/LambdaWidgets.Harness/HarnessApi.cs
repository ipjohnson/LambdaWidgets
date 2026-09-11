using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Dashboard;

namespace LambdaWidgets.Harness;

/// <summary>
/// Driving a widget from a language that is not C#.
/// </summary>
/// <remarks>
/// <para>
/// The framework's own driver is <c>IWidgetDriver</c> and does not go through this. This is for
/// everybody else: a Python or Node author writes a click-through with an HTTP client and nothing
/// else, against the same interpreter the page and the driver use — so a test in any language
/// asserts on the console's behaviour rather than on a private model of it.
/// </para>
/// <para>
/// That is the part worth having. Anyone can post JSON to a Lambda emulator; what nobody outside
/// this repository can do is work out what the console would send for a click, or what it would
/// have stripped before rendering.
/// </para>
/// </remarks>
[Handler]
public class HarnessApi(IHarnessRegistry registry) {

    /// <summary>Takes a dashboard body and answers the id to drive it by.</summary>
    [Post("/api/dashboards")]
    public DashboardCreated Create([FromBody] string dashboardBody) =>
        new(registry.Add(dashboardBody));

    /// <summary>Sets the dashboard's controls: theme, account, and the range widgets search over.</summary>
    [Put("/api/dashboards/{id}/state")]
    public DashboardStateView State(string id, [FromBody] DashboardStateRequest request) {
        var harness = registry.Get(id);

        harness.State = harness.State with {
            Name = request.Name ?? harness.State.Name,
            AccountId = request.AccountId ?? harness.State.AccountId,
            Theme = string.Equals(request.Theme, "dark", StringComparison.OrdinalIgnoreCase)
                ? Theme.Dark
                : Theme.Light,
            TimeRange = request.RelativeMinutes is > 0
                ? WidgetTimeRange.Relative(TimeSpan.FromMinutes(request.RelativeMinutes.Value))
                : harness.State.TimeRange
        };

        return Describe(harness);
    }

    [Get("/api/dashboards/{id}")]
    public DashboardStateView Read(string id) => Describe(registry.Get(id));

    /// <summary>Invokes a widget as the console does when it appears, and answers what it shows.</summary>
    [Get("/api/dashboards/{id}/widgets/{widgetId}")]
    public async Task<WidgetView> Open(string id, string widgetId, CancellationToken cancellationToken) =>
        Describe(await registry.Get(id).Open(widgetId, cancellationToken));

    /// <summary>What is clickable on a shown widget, by the text a viewer would read.</summary>
    [Get("/api/dashboards/{id}/widgets/{widgetId}/actions")]
    public IReadOnlyList<ActionView> Actions(string id, string widgetId) =>
        registry.Get(id).Shown.TryGetValue(widgetId, out var view)
            ? view.Shown.Actions.Select(action => new ActionView(
                action.Index, action.Text, action.Kind.ToString(), action.Display.ToString(),
                action.Confirmation, action.Event.ToString(), action.Parameters)).ToList()
            : [];

    /// <param name="fields">What is in the widget's form fields now. Untouched ones keep what the function rendered.</param>
    [Post("/api/dashboards/{id}/widgets/{widgetId}/actions/{index}")]
    public async Task<WidgetView> Click(
        string id,
        string widgetId,
        int index,
        [FromBody] Dictionary<string, string> fields,
        CancellationToken cancellationToken) =>
        Describe(await registry.Get(id).Click(widgetId, index, fields, cancellationToken));

    [Post("/api/dashboards/{id}/widgets/{widgetId}/describe")]
    public async Task<WidgetView> Describe(string id, string widgetId, CancellationToken cancellationToken) =>
        Describe(await registry.Get(id).Describe(widgetId, cancellationToken));

    /// <summary>Re-invokes every widget whose <c>updateOn</c> asked to hear about a refresh.</summary>
    [Post("/api/dashboards/{id}/refresh")]
    public async Task<DashboardStateView> Refresh(string id, CancellationToken cancellationToken) {
        var harness = registry.Get(id);

        await harness.RefreshAll(DashboardTrigger.Refresh, cancellationToken);

        return Describe(harness);
    }

    /// <summary>
    /// The linter, on HTML that has not been anywhere near a function.
    /// </summary>
    /// <remarks>
    /// Useful on its own: a widget author in any language can check what the console would do to
    /// their markup without deploying it, running it, or having an AWS account.
    /// </remarks>
    [Post("/api/lint")]
    public IReadOnlyList<FindingView> Lint(
        [FromBody] string html, [FromServices] IWidgetLinter linter) =>
        linter.Lint(html).Select(one => new FindingView(one.Rule, one.Message, one.Where)).ToList();

    private static DashboardStateView Describe(IWidgetHarness harness) =>
        new(harness.State.Name,
            harness.State.Theme.ToString(),
            harness.State.TimeRange.Effective.Start,
            harness.State.TimeRange.Effective.End,
            harness.Dashboard.Widgets.Select(one => one.Id).ToList());

    private static WidgetView Describe(WidgetRender view) =>
        new(view.Shown.Kind.ToString(),
            view.Shown.Html,
            view.Shown.Styles,
            view.Event,
            view.Invoke.Body,
            view.Invoke.FunctionError,
            view.Invoke.Duration.TotalMilliseconds,
            view.Shown.Forms,
            view.Shown.Actions.Select(action => new ActionView(
                action.Index, action.Text, action.Kind.ToString(), action.Display.ToString(),
                action.Confirmation, action.Event.ToString(), action.Parameters)).ToList(),
            view.Shown.Findings.Select(one => new FindingView(one.Rule, one.Message, one.Where)).ToList());
}

public record DashboardCreated(string Id);

public record DashboardStateRequest(
    string? Name, string? AccountId, string? Theme, int? RelativeMinutes);

public record DashboardStateView(
    string Name, string Theme, DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<string> Widgets);

/// <summary>
/// One widget, as the HTTP API reports it.
/// </summary>
/// <param name="Kind">Whether the function answered with <c>Html</c>, <c>Markdown</c> or <c>Json</c>.</param>
/// <param name="Raw">The Invoke response as it came back, for a test asserting on the wire.</param>
/// <param name="Event">The JSON the console would have sent, which is the thing hardest to get right.</param>
/// <param name="FunctionError">
/// What the Invoke API's <c>X-Amz-Function-Error</c> header said, or null. This is the function
/// failing to answer at all; a handler that threw and was answered for shows up as a
/// <c>invocation-failed</c> finding instead.
/// </param>
public record WidgetView(
    string Kind,
    string Html,
    string Styles,
    string Event,
    string Raw,
    string? FunctionError,
    double Milliseconds,
    IReadOnlyDictionary<string, string> Forms,
    IReadOnlyList<ActionView> Actions,
    IReadOnlyList<FindingView> Findings);

public record ActionView(
    int Index,
    string Text,
    string Kind,
    string Display,
    string? Confirmation,
    string Event,
    IReadOnlyDictionary<string, string> Parameters);

public record FindingView(string Rule, string Message, string Where);
