using System.Text.Json;

namespace LambdaWidgets.Dashboard;

/// <summary>How the console displays what a function returned.</summary>
public enum ResponseKind {
    /// <summary>Rendered as HTML, after the sanitizer.</summary>
    Html,

    /// <summary>Rendered as markdown. The function returned <c>{"markdown": "..."}</c>.</summary>
    Markdown,

    /// <summary>Displayed as JSON, which is what anything else becomes.</summary>
    Json
}

/// <summary>What a function returned, and how to show it.</summary>
public sealed record WidgetResponse(ResponseKind Kind, string Content);

/// <summary>
/// Reads an Invoke response body the way the console reads it.
/// </summary>
public interface IWidgetResponses {
    /// <summary>Classifies a raw Invoke response body the way the console reads it.</summary>
    WidgetResponse Classify(string body);
}

/// <inheritdoc />
public sealed class WidgetResponses : IWidgetResponses {
    private const string MarkdownField = "markdown";

    /// <summary>
    /// Classifies a raw Invoke response body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body is JSON, because that is what the Lambda Invoke API returns. A function returning
    /// an HTML string therefore arrives here as a JSON string with the HTML inside it, quoted and
    /// escaped, which is the thing that surprises anyone reading a raw response for the first time.
    /// Unwrapping that is most of this method.
    /// </para>
    /// <para>
    /// An object with a <c>markdown</c> field is markdown. Anything else is shown as JSON, which is
    /// the console's answer to a function that returned a shape it does not recognise: display it
    /// rather than fail, so the author can see what came back.
    /// </para>
    /// </remarks>
    public WidgetResponse Classify(string body) {
        if (string.IsNullOrWhiteSpace(body)) {
            return new WidgetResponse(ResponseKind.Html, "");
        }

        JsonDocument document;

        try {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException) {
            // Not JSON at all. The Invoke API would not do this, but a proxy or a fixture might,
            // and the useful answer is to render it rather than to report a parse failure.
            return new WidgetResponse(ResponseKind.Html, body);
        }

        using (document) {
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String) {
                return new WidgetResponse(ResponseKind.Html, root.GetString() ?? "");
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(MarkdownField, out var markdown) &&
                markdown.ValueKind == JsonValueKind.String) {
                return new WidgetResponse(ResponseKind.Markdown, markdown.GetString() ?? "");
            }

            return new WidgetResponse(ResponseKind.Json, Indented(root));
        }
    }

    /// <remarks>Indented, because the console displays JSON to be read rather than to be parsed.</remarks>
    private static string Indented(JsonElement element) {
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true })) {
            element.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
