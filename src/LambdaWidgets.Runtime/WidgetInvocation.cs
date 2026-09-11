using System.Text.Json;

namespace LambdaWidgets.Runtime;

/// <summary>
/// One widget invocation, read out of the payload the console sent.
/// </summary>
/// <remarks>
/// <para>
/// Parsed by hand with <c>Utf8JsonReader</c>'s document form rather than bound to a type through a
/// serializer. The event's top-level fields are open — a <c>cwdb-action</c> may send any names its
/// author chose — so there is no type to bind, and a <c>JsonSerializerContext</c> covering the
/// context object alone would still leave those to read out by hand.
/// </para>
/// <para>
/// Nothing here uses reflection, which is what keeps a widget publishable ahead of time.
/// </para>
/// </remarks>
internal sealed class WidgetInvocation {
    /// <summary>
    /// The reserved top-level field naming the handler to run.
    /// </summary>
    /// <remarks>
    /// The framework documents the same field for the same job, on
    /// <c>InvokeAdapter.OperationField</c>: "one function, many operations, selected by a field the
    /// caller sets". A dashboard author who wants a widget to open on a specific tool sets it in
    /// the widget's parameters.
    /// </remarks>
    public const string RouteField = "route";

    /// <summary>The field the console sets when it wants documentation rather than a render.</summary>
    public const string DescribeField = "describe";

    private WidgetInvocation(
        string route,
        IReadOnlyDictionary<string, WidgetValue> query,
        WidgetContext context,
        bool describe) {
        Route = route;
        Query = query;
        Context = context;
        Describe = describe;
    }

    /// <summary>The path the route table matches, from <see cref="RouteField"/>.</summary>
    public string Route { get; }

    /// <summary>Everything a handler can bind, merged.</summary>
    public IReadOnlyDictionary<string, WidgetValue> Query { get; }

    public WidgetContext Context { get; }

    public bool Describe { get; }

    /// <summary>
    /// Reads an invocation from the payload's bytes.
    /// </summary>
    /// <remarks>
    /// <b>The merge order is the whole of parameter binding for a widget, and it is not arbitrary.</b>
    /// <c>widgetContext.params</c> first, then the event's top-level fields, then
    /// <c>widgetContext.forms.all</c>, each overwriting the last. That is the AWS sample's
    /// <c>form.query || event.query</c> generalised: what the viewer typed beats what an action
    /// sent, which beats how the widget was configured. A handler binds one request object from the
    /// result and never has to know which of the three a value came from.
    /// </remarks>
    public static WidgetInvocation Read(ReadOnlySpan<byte> payload) {
        using var document = JsonDocument.Parse(payload.ToArray());

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new InvalidOperationException(
                "A widget invocation is a JSON object carrying widgetContext. This payload is " +
                $"{root.ValueKind}, so either something other than the console invoked this " +
                "function, or it was wired to an event source no handler asked for.");
        }

        var query = new Dictionary<string, WidgetValue>(StringComparer.OrdinalIgnoreCase);

        var context = root.TryGetProperty("widgetContext", out var element) &&
                      element.ValueKind == JsonValueKind.Object
            ? WidgetContext.Read(element)
            : WidgetContext.Absent;

        foreach (var configured in context.RawParams) {
            query[configured.Key] = configured.Value;
        }


        var route = "/";
        var describe = false;

        foreach (var field in root.EnumerateObject()) {
            if (field.NameEquals("widgetContext")) {
                continue;
            }

            if (field.NameEquals(DescribeField)) {
                // Present at all means asked, whatever it holds. The console sends true; a caller
                // testing by hand sends whatever is convenient, and refusing theirs would be a
                // distinction with nothing behind it.
                describe = field.Value.ValueKind != JsonValueKind.False;

                continue;
            }

            var value = WidgetValue.From(field.Value);

            if (field.NameEquals(RouteField)) {
                route = Path(value.Text);

                continue;
            }

            query[field.Name] = value;
        }

        foreach (var typed in context.RawForms) {
            query[typed.Key] = typed.Value;
        }

        return new WidgetInvocation(route, query, context, describe);
    }

    /// <remarks>
    /// <para>
    /// A route arriving without its leading slash is the author's likely mistake rather than a
    /// different route, and the route table has one form. Repairing it here beats a 404 that names
    /// a path the author believes they wrote.
    /// </para>
    /// <para>
    /// The path stays escaped. The route table matches it as it arrived and
    /// <see cref="WidgetRequest.PathTokens"/> decodes each captured value, because decoding here
    /// would turn an escaped separator into a real one and a key holding a slash would stop
    /// matching its own route.
    /// </para>
    /// </remarks>
    private static string Path(string route) =>
        string.IsNullOrWhiteSpace(route) ? "/"
        : route[0] == '/' ? route
        : "/" + route;

    /// <summary>
    /// A field's value as the string a handler will bind from.
    /// </summary>
    /// <remarks>
    /// Everything becomes a string, because everything reaches a handler through the query string
    /// and the ordinary binding parses it back. A nested object or array keeps its JSON text, which
    /// is the only lossless answer for a shape the transport has no other way to carry.
    /// </remarks>
    internal static string Scalar(JsonElement value) => WidgetValue.From(value).Text;
}

/// <summary>
/// One value on its way to a handler, as text and as JSON.
/// </summary>
/// <remarks>
/// <para>
/// A widget carries values two ways at once and they want different forms. The query string is
/// strings, because that is what a query string is and what <c>[FromQueryString]</c> converts from.
/// The body is JSON, because a complex parameter is deserialized rather than converted, and
/// deserializing <c>{"limit":"20"}</c> into an <c>int</c> works only because the framework's
/// deserializer defaults to <c>AllowReadingFromString</c> — while <c>{"live":"true"}</c> into a
/// <c>bool</c> does not.
/// </para>
/// <para>
/// So a value that arrived as a JSON number or boolean keeps that shape on the way out, and only a
/// value that was genuinely text is written as text. A form field is always text: a checkbox sends
/// <c>on</c> rather than <c>true</c>, so a widget binding one to a <c>bool</c> needs a converter
/// whatever this does.
/// </para>
/// </remarks>
internal readonly record struct WidgetValue(string Text, bool IsJsonLiteral) {
    public static WidgetValue From(JsonElement value) => value.ValueKind switch {
        JsonValueKind.String => new WidgetValue(value.GetString()!, false),
        JsonValueKind.Null or JsonValueKind.Undefined => new WidgetValue("", false),
        JsonValueKind.True => new WidgetValue("true", true),
        JsonValueKind.False => new WidgetValue("false", true),
        // A number, object or array keeps its JSON text, which is the only lossless answer for a
        // shape the query string has no way to carry.
        _ => new WidgetValue(value.GetRawText(), true)
    };

    /// <summary>Writes this value into a JSON object under <paramref name="name"/>.</summary>
    public void Write(System.Text.Json.Utf8JsonWriter writer, string name) {
        if (IsJsonLiteral) {
            writer.WritePropertyName(name);
            writer.WriteRawValue(Text);
        }
        else {
            writer.WriteString(name, Text);
        }
    }
}
