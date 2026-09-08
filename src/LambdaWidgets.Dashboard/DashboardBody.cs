using System.Text;
using System.Text.Json;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// One custom widget on a dashboard, as its entry in the dashboard body describes it.
/// </summary>
/// <remarks>
/// Only the custom widget type is modelled. A dashboard holding metric graphs and alarms parses,
/// and those entries are carried through <see cref="DashboardBody.Write"/> untouched rather than
/// understood, so the harness can open a real dashboard file and write it back without dropping
/// what it does not render.
/// </remarks>
public sealed record DashboardWidget {
    /// <summary>The widget's id within the dashboard. The console sends it as <c>widgetId</c>.</summary>
    /// <remarks>
    /// The console's own ids look like <c>widget-16</c>. A dashboard body does not carry one, so
    /// this is assigned by position on parse and is stable for as long as the file is.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>The function ARN the console invokes, from <c>properties.endpoint</c>.</summary>
    public required string Endpoint { get; init; }

    public string? Title { get; init; }

    /// <summary>The widget's configured parameters, from <c>properties.params</c>.</summary>
    /// <remarks>
    /// Strings, whatever the JSON said. Every value makes the round trip to a handler as a query
    /// string value and comes back through the binding, so holding a number as a number here would
    /// be a fidelity the transport does not have.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Params { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Which dashboard events re-invoke the widget, from <c>properties.updateOn</c>.
    /// </summary>
    public UpdateOn UpdateOn { get; init; } = UpdateOn.Default;

    public int Width { get; init; } = 6;

    public int Height { get; init; } = 6;
}

/// <summary>
/// The triggers that re-invoke a widget with its configured parameters.
/// </summary>
/// <remarks>
/// The console's defaults for the three keys are <b>unverified</b> until day-one check 3 runs. All
/// three default to true here, which is what the documented behaviour describes: a custom widget
/// refreshes, resizes and follows the time range like any other widget.
/// </remarks>
public readonly record struct UpdateOn(bool Refresh, bool Resize, bool TimeRange) {
    public static readonly UpdateOn Default = new(true, true, true);
}

/// <summary>
/// A CloudWatch dashboard body, the JSON <c>PutDashboard</c> takes.
/// </summary>
/// <remarks>
/// <para>
/// The harness loads one of these from a file, renders the custom widgets in it, and writes the
/// same file back when a widget is edited. Section 10 deploys that file as the dashboard, so the
/// file developed against and the file deployed are one artifact.
/// </para>
/// <para>
/// The exact shape of a custom widget entry is <b>unverified</b> until day-one check 3 reads one
/// out of a real dashboard. What is here follows the documented <c>PutDashboard</c> schema.
/// </para>
/// </remarks>
public sealed class DashboardBody {
    private readonly string _source;

    private DashboardBody(string source, IReadOnlyList<DashboardWidget> widgets) {
        _source = source;
        Widgets = widgets;
    }

    /// <summary>The custom widgets, in the order the body lists them.</summary>
    public IReadOnlyList<DashboardWidget> Widgets { get; }

    internal static DashboardBody Parse(string json) {
        using var document = JsonDocument.Parse(json);

        var widgets = new List<DashboardWidget>();

        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("widgets", out var entries) ||
            entries.ValueKind != JsonValueKind.Array) {
            return new DashboardBody(json, widgets);
        }

        var index = 0;

        foreach (var entry in entries.EnumerateArray()) {
            index++;

            if (!IsCustomWidget(entry, out var properties)) {
                continue;
            }

            widgets.Add(Read(entry, properties, index));
        }

        return new DashboardBody(json, widgets);
    }

    /// <summary>
    /// The body as it was read, which is what the harness writes back.
    /// </summary>
    /// <remarks>
    /// Editing a widget is not implemented here yet, so this is the source text rather than a
    /// re-serialization. Returning the source is the honest form of that: a re-serialization would
    /// silently reorder keys and drop the entries this type does not model.
    /// </remarks>
    public string Write() => _source;

    /// <remarks>
    /// A custom widget is <c>type: "custom"</c> with an <c>endpoint</c> under its properties. The
    /// type alone is not enough: the console uses <c>custom</c> for its own widget kinds too, and
    /// an entry with no endpoint is not something this can invoke.
    /// </remarks>
    private static bool IsCustomWidget(JsonElement entry, out JsonElement properties) {
        properties = default;

        return entry.ValueKind == JsonValueKind.Object &&
               entry.TryGetProperty("type", out var type) &&
               type.ValueKind == JsonValueKind.String &&
               type.ValueEquals("custom") &&
               entry.TryGetProperty("properties", out properties) &&
               properties.ValueKind == JsonValueKind.Object &&
               properties.TryGetProperty("endpoint", out var endpoint) &&
               endpoint.ValueKind == JsonValueKind.String;
    }

    private static DashboardWidget Read(JsonElement entry, JsonElement properties, int index) =>
        new() {
            Id = "widget-" + index,
            Endpoint = properties.GetProperty("endpoint").GetString()!,
            Title = String(properties, "title"),
            Params = ReadParams(properties),
            UpdateOn = ReadUpdateOn(properties),
            Width = Number(entry, "width", 6),
            Height = Number(entry, "height", 6)
        };

    /// <remarks>
    /// Every value becomes a string, because that is what it will be by the time a handler binds
    /// it. A nested object or array is written back as its JSON text rather than flattened: the
    /// console sends the parameters through as they were configured, and a widget that configured
    /// a list should receive one.
    /// </remarks>
    private static Dictionary<string, string> ReadParams(JsonElement properties) {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!properties.TryGetProperty("params", out var declared) ||
            declared.ValueKind != JsonValueKind.Object) {
            return values;
        }

        foreach (var value in declared.EnumerateObject()) {
            values[value.Name] = value.Value.ValueKind switch {
                JsonValueKind.String => value.Value.GetString()!,
                JsonValueKind.Null => "",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => value.Value.GetRawText()
            };
        }

        return values;
    }

    private static UpdateOn ReadUpdateOn(JsonElement properties) {
        if (!properties.TryGetProperty("updateOn", out var declared) ||
            declared.ValueKind != JsonValueKind.Object) {
            return UpdateOn.Default;
        }

        return new UpdateOn(
            Flag(declared, "refresh"),
            Flag(declared, "resize"),
            Flag(declared, "timeRange"));
    }

    /// <remarks>Absent means on, which is what a widget with no <c>updateOn</c> at all does.</remarks>
    private static bool Flag(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) ||
        value.ValueKind != JsonValueKind.False;

    private static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Number(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var number)
            ? number
            : fallback;
}


/// <summary>
/// Reads CloudWatch dashboard JSON.
/// </summary>
public interface IDashboardBodies {
    /// <summary>The custom widgets in a <c>PutDashboard</c> body.</summary>
    DashboardBody Read(string json);
}

/// <inheritdoc />
public sealed class DashboardBodies : IDashboardBodies {
    public DashboardBody Read(string json) => DashboardBody.Parse(json);
}
