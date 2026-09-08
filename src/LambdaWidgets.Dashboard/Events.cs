using System.Text;
using System.Text.Json;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// One invocation's payload: the top-level fields and the <c>widgetContext</c> beside them.
/// </summary>
/// <remarks>
/// Held as its parts rather than as JSON text so a test can assert on a field without parsing, and
/// written with <see cref="ToJson"/> when it goes to a function. The harness's inspector shows both.
/// </remarks>
public sealed record WidgetEvent {
    /// <summary>
    /// The fields the console puts at the root: the widget's configured parameters, and anything a
    /// <c>cwdb-action</c> sent.
    /// </summary>
    /// <remarks>
    /// A parameter sent by an action arrives here rather than inside <c>widgetContext.params</c>,
    /// which is what lets a handler tell the two apart.
    /// </remarks>
    public required IReadOnlyDictionary<string, string> Fields { get; init; }

    public required WidgetContext Context { get; init; }

    /// <summary>Set when the console is asking for documentation rather than for a render.</summary>
    public bool Describe { get; init; }

    /// <summary>The payload, as the Invoke API would carry it.</summary>
    public string ToJson() {
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();

            foreach (var field in Fields) {
                writer.WriteString(field.Key, field.Value);
            }

            if (Describe) {
                writer.WriteBoolean("describe", true);
            }

            Context.Write(writer);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}

/// <summary>
/// <c>widgetContext</c>, the object every invocation carries.
/// </summary>
public sealed record WidgetContext {
    public required string DashboardName { get; init; }

    public required string WidgetId { get; init; }

    public required string AccountId { get; init; }

    public required string Locale { get; init; }

    public required WidgetTimezone Timezone { get; init; }

    public required Period Period { get; init; }

    public required WidgetTimeRange TimeRange { get; init; }

    public required Theme Theme { get; init; }

    public bool LinkCharts { get; init; }

    public string? Title { get; init; }

    /// <summary>
    /// The widget's form fields, keyed by <c>name</c>, under <c>forms.all</c>.
    /// </summary>
    /// <remarks>Empty on every invocation the user did not cause by clicking a bound element.</remarks>
    public IReadOnlyDictionary<string, string> Forms { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The widget's configured parameters, repeated here as the console repeats them.
    /// </summary>
    /// <remarks>
    /// The same values are at the root of the event. A handler reads whichever it wants; the plan's
    /// merge in section 5.2 takes the root and the forms over these, which is the AWS sample's
    /// <c>form.query || event.query</c> generalised.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Params { get; init; } =
        new Dictionary<string, string>();

    public int Width { get; init; } = 588;

    public int Height { get; init; } = 369;

    internal void Write(Utf8JsonWriter writer) {
        writer.WriteStartObject("widgetContext");

        writer.WriteString("dashboardName", DashboardName);
        writer.WriteString("widgetId", WidgetId);
        writer.WriteString("accountId", AccountId);
        writer.WriteString("locale", Locale);

        writer.WriteStartObject("timezone");
        writer.WriteString("label", Timezone.Label);
        writer.WriteString("offsetISO", Timezone.OffsetIso);
        writer.WriteNumber("offsetInMinutes", Timezone.OffsetInMinutes);
        writer.WriteEndObject();

        writer.WriteNumber("period", Period.Seconds);
        writer.WriteBoolean("isAutoPeriod", Period.IsAutomatic);

        WriteTimeRange(writer);

        writer.WriteString("theme", Theme == Theme.Dark ? "dark" : "light");
        writer.WriteBoolean("linkCharts", LinkCharts);
        writer.WriteString("title", Title ?? "");

        writer.WriteStartObject("forms");
        WriteMap(writer, "all", Forms);
        writer.WriteEndObject();

        WriteMap(writer, "params", Params);

        writer.WriteNumber("width", Width);
        writer.WriteNumber("height", Height);

        writer.WriteEndObject();
    }

    /// <remarks>
    /// <c>relativeStart</c> is written only for a relative range and <c>zoom</c> only when the user
    /// has zoomed, because that is when the console writes them. A widget reading
    /// <c>timeRange.zoom || timeRange</c> against an always-present zoom would never see the
    /// dashboard's own range.
    /// </remarks>
    private void WriteTimeRange(Utf8JsonWriter writer) {
        writer.WriteStartObject("timeRange");

        writer.WriteString("mode", TimeRange.Mode == TimeRangeMode.Absolute ? "absolute" : "relative");
        writer.WriteNumber("start", TimeRange.Start.ToUnixTimeMilliseconds());
        writer.WriteNumber("end", TimeRange.End.ToUnixTimeMilliseconds());

        if (TimeRange.RelativeStart is { } relative) {
            writer.WriteNumber("relativeStart", (long)relative.TotalMilliseconds);
        }

        if (TimeRange.Zoom is { } zoom) {
            writer.WriteStartObject("zoom");
            writer.WriteNumber("start", zoom.Start.ToUnixTimeMilliseconds());
            writer.WriteNumber("end", zoom.End.ToUnixTimeMilliseconds());
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteMap(
        Utf8JsonWriter writer, string name, IReadOnlyDictionary<string, string> values) {
        writer.WriteStartObject(name);

        foreach (var value in values) {
            writer.WriteString(value.Key, value.Value);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// What the console sends, and when.
/// </summary>
public interface IWidgetEvents {
    /// <summary>The event for a load, a refresh, a resize or a time range change.</summary>
    WidgetEvent Initial(DashboardWidget widget, DashboardState state);

    /// <summary>The event for a <c>cwdb-action</c> the viewer fired.</summary>
    WidgetEvent ForAction(
        WidgetAction action,
        IReadOnlyDictionary<string, string> forms,
        DashboardWidget widget,
        DashboardState state);

    /// <summary>The event behind the console's <em>Get documentation</em> button.</summary>
    WidgetEvent Describe(DashboardWidget widget, DashboardState state);
}

/// <inheritdoc />
public sealed class WidgetEvents : IWidgetEvents {
    /// <summary>
    /// The event for a load, a refresh, a resize or a time range change.
    /// </summary>
    /// <remarks>
    /// <b>The widget's configured parameters, and nothing else.</b> This is the behaviour that
    /// surprises a widget author: every one of those triggers sends the same event as the first
    /// load, so a widget the user navigated three pages into returns to its landing page when the
    /// dashboard refreshes. The harness reproduces it rather than smoothing it over, because the
    /// console does it and a widget has to be designed for it.
    /// </remarks>
    public WidgetEvent Initial(DashboardWidget widget, DashboardState state) =>
        new() {
            Fields = new Dictionary<string, string>(widget.Params, StringComparer.Ordinal),
            Context = Context(widget, state, forms: new Dictionary<string, string>())
        };

    /// <summary>
    /// The event for a <c>cwdb-action</c> the user fired.
    /// </summary>
    /// <remarks>
    /// The action's JSON goes over the configured parameters at the root, and the widget's form
    /// fields go under <c>forms.all</c>. Both are what the console sends; which one a handler reads
    /// is the handler's business.
    /// </remarks>
    public WidgetEvent ForAction(
        WidgetAction action,
        IReadOnlyDictionary<string, string> forms,
        DashboardWidget widget,
        DashboardState state) {
        var fields = new Dictionary<string, string>(widget.Params, StringComparer.Ordinal);

        foreach (var parameter in action.Parameters) {
            fields[parameter.Key] = parameter.Value;
        }

        return new WidgetEvent {
            Fields = fields,
            Context = Context(widget, state, forms)
        };
    }

    /// <summary>
    /// The event behind the console's <em>Get documentation</em> button.
    /// </summary>
    /// <remarks>
    /// The configured parameters ride along, as they do on every invocation. A function answering
    /// describe is expected to ignore them and return markdown.
    /// </remarks>
    public WidgetEvent Describe(DashboardWidget widget, DashboardState state) =>
        Initial(widget, state) with { Describe = true };

    private static WidgetContext Context(
        DashboardWidget widget,
        DashboardState state,
        IReadOnlyDictionary<string, string> forms) =>
        new() {
            DashboardName = state.Name,
            WidgetId = widget.Id,
            AccountId = state.AccountId,
            Locale = state.Locale,
            Timezone = state.Timezone,
            Period = state.Period,
            TimeRange = state.TimeRange,
            Theme = state.Theme,
            LinkCharts = state.LinkCharts,
            Title = widget.Title,
            Forms = forms,
            Params = widget.Params,
            Width = widget.Width,
            Height = widget.Height
        };
}
