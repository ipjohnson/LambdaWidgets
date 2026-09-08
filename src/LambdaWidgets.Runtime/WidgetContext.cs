using System.Text.Json;

namespace LambdaWidgets.Runtime;

/// <summary>The dashboard's colour scheme, which arrives as <c>widgetContext.theme</c>.</summary>
public enum WidgetTheme {
    Light,
    Dark
}

/// <summary>
/// The dashboard's time range, as <c>widgetContext.timeRange</c> carries it.
/// </summary>
/// <param name="Start">The range's start. Epoch milliseconds on the wire.</param>
/// <param name="End">The range's end.</param>
/// <param name="Zoom">Present only when the viewer has zoomed a chart.</param>
public readonly record struct WidgetTimeRange(
    DateTimeOffset Start,
    DateTimeOffset End,
    (DateTimeOffset Start, DateTimeOffset End)? Zoom) {
    /// <summary>
    /// The range a handler should query over.
    /// </summary>
    /// <remarks>
    /// <b>Read this rather than <see cref="Start"/>.</b> The AWS sample takes
    /// <c>timeRange.zoom || timeRange</c>, and a widget reading the range directly ignores the
    /// zoom the viewer just did — which reads as the widget beside the chart refusing to follow it.
    /// </remarks>
    public (DateTimeOffset Start, DateTimeOffset End) Effective => Zoom ?? (Start, End);
}

/// <summary>
/// The typed <c>widgetContext</c>, available to a handler as a scoped service.
/// </summary>
/// <remarks>
/// A handler takes it as a parameter or a constructor dependency. Everything a widget needs about
/// where it is running is here, so a tool follows the dashboard's time picker and theme without a
/// form field of its own.
/// </remarks>
public interface IWidgetContext {
    string DashboardName { get; }

    string WidgetId { get; }

    string AccountId { get; }

    string Locale { get; }

    /// <summary>The dashboard's period in seconds, and whether the console chose it.</summary>
    (int Seconds, bool IsAutomatic) Period { get; }

    WidgetTimeRange TimeRange { get; }

    WidgetTheme Theme { get; }

    string Title { get; }

    int Width { get; }

    int Height { get; }

    /// <summary>The widget's configured parameters, as the dashboard holds them.</summary>
    IReadOnlyDictionary<string, string> Params { get; }

    /// <summary>The form fields that travelled with a click, from <c>forms.all</c>.</summary>
    IReadOnlyDictionary<string, string> Forms { get; }

    /// <summary>
    /// The ARN this function was invoked as, including any alias qualifier.
    /// </summary>
    /// <remarks>
    /// What the Razor helpers write into a <c>cwdb-action</c>'s <c>endpoint</c>, so a widget calls
    /// back into the exact function and alias the viewer reached. Hard-coding an ARN in a template
    /// is what makes a widget stop working when it is deployed to a second account.
    /// </remarks>
    string InvokedFunctionArn { get; }
}

/// <inheritdoc />
internal sealed class WidgetContext : IWidgetContext {
    private static readonly Dictionary<string, string> Nothing = new();

    /// <summary>
    /// What a payload with no <c>widgetContext</c> gets.
    /// </summary>
    /// <remarks>
    /// Defaults rather than a throw. The adapter has already decided this payload is a widget's, and
    /// a handler asking for the dashboard's theme when the caller sent none should get "light"
    /// rather than a failed invocation — a hand-written test payload is the common case.
    /// </remarks>
    public static WidgetContext Absent { get; } = new();

    public string DashboardName { get; private init; } = "";

    public string WidgetId { get; private init; } = "";

    public string AccountId { get; private init; } = "";

    public string Locale { get; private init; } = "en";

    public (int Seconds, bool IsAutomatic) Period { get; private init; } = (300, true);

    public WidgetTimeRange TimeRange { get; private init; } =
        new(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);

    public WidgetTheme Theme { get; private init; } = WidgetTheme.Light;

    public string Title { get; private init; } = "";

    public int Width { get; private init; }

    public int Height { get; private init; }

    public IReadOnlyDictionary<string, string> Params { get; private init; } = Nothing;

    public IReadOnlyDictionary<string, string> Forms { get; private init; } = Nothing;

    /// <summary>Filled by the adapter from <c>ILambdaContext</c>, which the payload does not carry.</summary>
    public string InvokedFunctionArn { get; internal set; } = "";

    /// <summary>The raw elements behind <see cref="Params"/>, kept so the merge can preserve types.</summary>
    internal Dictionary<string, WidgetValue> RawParams { get; private init; } = new();

    /// <summary>The raw elements behind <see cref="Forms"/>. Always text; a form field has no type.</summary>
    internal Dictionary<string, WidgetValue> RawForms { get; private init; } = new();

    internal static WidgetContext Read(JsonElement element) => new() {
        DashboardName = String(element, "dashboardName"),
        WidgetId = String(element, "widgetId"),
        AccountId = String(element, "accountId"),
        Locale = String(element, "locale") is { Length: > 0 } locale ? locale : "en",
        Period = (Number(element, "period", 300), Flag(element, "isAutoPeriod", true)),
        TimeRange = ReadTimeRange(element),
        Theme = string.Equals(String(element, "theme"), "dark", StringComparison.OrdinalIgnoreCase)
            ? WidgetTheme.Dark
            : WidgetTheme.Light,
        Title = String(element, "title"),
        Width = Number(element, "width", 0),
        Height = Number(element, "height", 0),
        Params = Map(element, "params"),
        Forms = ReadForms(element),
        RawParams = RawMap(element, "params"),
        RawForms = element.TryGetProperty("forms", out var forms) && forms.ValueKind == JsonValueKind.Object
            ? RawMap(forms, "all")
            : new Dictionary<string, WidgetValue>()
    };

    private static Dictionary<string, WidgetValue> RawMap(JsonElement element, string name) {
        var values = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);

        if (!element.TryGetProperty(name, out var declared) ||
            declared.ValueKind != JsonValueKind.Object) {
            return values;
        }

        foreach (var value in declared.EnumerateObject()) {
            values[value.Name] = WidgetValue.From(value.Value);
        }

        return values;
    }

    private static WidgetTimeRange ReadTimeRange(JsonElement element) {
        if (!element.TryGetProperty("timeRange", out var range) ||
            range.ValueKind != JsonValueKind.Object) {
            return new WidgetTimeRange(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);
        }

        (DateTimeOffset, DateTimeOffset)? zoom = null;

        if (range.TryGetProperty("zoom", out var zoomed) && zoomed.ValueKind == JsonValueKind.Object) {
            zoom = (Instant(zoomed, "start"), Instant(zoomed, "end"));
        }

        return new WidgetTimeRange(Instant(range, "start"), Instant(range, "end"), zoom);
    }

    /// <summary>The fields under <c>forms.all</c>, which is where a click's form values arrive.</summary>
    private static IReadOnlyDictionary<string, string> ReadForms(JsonElement element) =>
        element.TryGetProperty("forms", out var forms) && forms.ValueKind == JsonValueKind.Object
            ? Map(forms, "all")
            : Nothing;

    private static DateTimeOffset Instant(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : DateTimeOffset.UnixEpoch;

    private static Dictionary<string, string> Map(JsonElement element, string name) {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!element.TryGetProperty(name, out var declared) ||
            declared.ValueKind != JsonValueKind.Object) {
            return values;
        }

        foreach (var value in declared.EnumerateObject()) {
            values[value.Name] = WidgetInvocation.Scalar(value.Value);
        }

        return values;
    }

    private static string String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static int Number(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : fallback;

    private static bool Flag(JsonElement element, string name, bool fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
}
