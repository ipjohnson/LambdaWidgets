using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// A fault in a <c>cwdb-action</c> that the console does not report.
/// </summary>
/// <remarks>
/// Every one of these produces an element that renders and does nothing when clicked, which is the
/// worst failure mode a widget has: it looks finished. The linter in section 7.4 surfaces them and
/// the test driver can fail on them.
/// </remarks>
public enum ActionFault {
    /// <summary>Nothing precedes the action, so there is no element to bind the behaviour to.</summary>
    NoBoundElement,

    /// <summary><c>action="call"</c> with no <c>endpoint</c>, so there is no function to invoke.</summary>
    CallWithoutEndpoint,

    /// <summary><c>event="mouseenter"</c> on a call. The console allows it only with <c>html</c>.</summary>
    MouseEnterOnCall,

    /// <summary>A call whose content is not a JSON object, so no parameters can be read from it.</summary>
    CallContentIsNotJson
}

/// <summary>
/// One <c>cwdb-action</c> and the element it binds.
/// </summary>
public sealed record WidgetAction {
    /// <summary>Its position among the actions in the document, which is how a driver names it.</summary>
    public required int Index { get; init; }

    /// <summary>The text of the element the action binds, which is how a driver finds it by name.</summary>
    /// <remarks>Empty when the bound element has no text, or when there is no bound element.</remarks>
    public required string Text { get; init; }

    public required ActionKind Kind { get; init; }

    public required Display Display { get; init; }

    public required ActionEvent Event { get; init; }

    /// <summary>The function ARN for a call.</summary>
    public string? Endpoint { get; init; }

    /// <summary>A message the user must acknowledge before the action fires.</summary>
    public string? Confirmation { get; init; }

    /// <summary>The parameters a call sends, read from the element's JSON content.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();

    /// <summary>The HTML an <see cref="ActionKind.Html"/> action displays.</summary>
    public string Html { get; init; } = "";

    public IReadOnlyList<ActionFault> Faults { get; init; } = Array.Empty<ActionFault>();

    /// <summary>The function name a call invokes, the segment after <c>function:</c> in the ARN.</summary>
    /// <remarks>
    /// This is what selects an invoke target in the harness. An endpoint that is not an ARN is
    /// taken as a name already, so a locally written fixture does not have to invent an account id.
    /// </remarks>
    public string? FunctionName {
        get {
            if (Endpoint is null) {
                return null;
            }

            var marker = Endpoint.LastIndexOf("function:", StringComparison.Ordinal);

            if (marker < 0) {
                return Endpoint;
            }

            var name = Endpoint[(marker + "function:".Length)..];

            // An alias or version qualifier follows the name after a colon, and the target is
            // chosen by the name alone.
            var qualifier = name.IndexOf(':');

            return qualifier < 0 ? name : name[..qualifier];
        }
    }
}

/// <summary>
/// Reads the <c>cwdb-action</c> elements out of a widget's HTML.
/// </summary>
public interface IWidgetActions {
    /// <summary>
    /// Every action in the document, in document order.
    /// </summary>
    /// <remarks>
    /// <b>An action binds the element immediately before it</b>, which is the rule the whole
    /// contract rests on and the one an author gets wrong. <c>PreviousElementSibling</c> is exactly
    /// that: whitespace and comments between the two do not break the binding, and another element
    /// does.
    /// </remarks>
    IReadOnlyList<WidgetAction> In(string html);
}

/// <inheritdoc />
public sealed class WidgetActions : IWidgetActions {
    private const string ActionElement = "cwdb-action";

    public IReadOnlyList<WidgetAction> In(string html) {
        var document = new HtmlParser().ParseDocument(html);

        var actions = new List<WidgetAction>();
        var index = 0;

        foreach (var element in document.QuerySelectorAll(ActionElement)) {
            actions.Add(Read(element, index++));
        }

        return actions;
    }

    private static WidgetAction Read(IElement element, int index) {
        var faults = new List<ActionFault>();

        var bound = element.PreviousElementSibling;

        if (bound is null) {
            faults.Add(ActionFault.NoBoundElement);
        }

        var kind = Attribute(element, "action") switch {
            "call" => ActionKind.Call,
            _ => ActionKind.Html
        };

        var display = Attribute(element, "display") switch {
            "popup" => Display.Popup,
            _ => Display.Widget
        };

        var fired = Attribute(element, "event") switch {
            "dblclick" => ActionEvent.DblClick,
            "mouseenter" => ActionEvent.MouseEnter,
            _ => ActionEvent.Click
        };

        var endpoint = Attribute(element, "endpoint");

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        if (kind == ActionKind.Call) {
            if (string.IsNullOrWhiteSpace(endpoint)) {
                faults.Add(ActionFault.CallWithoutEndpoint);
            }

            if (fired == ActionEvent.MouseEnter) {
                faults.Add(ActionFault.MouseEnterOnCall);
            }

            if (!ReadParameters(element.TextContent, parameters)) {
                faults.Add(ActionFault.CallContentIsNotJson);
            }
        }

        return new WidgetAction {
            Index = index,
            Text = bound?.TextContent.Trim() ?? "",
            Kind = kind,
            Display = display,
            Event = fired,
            Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            Confirmation = Attribute(element, "confirmation") is { Length: > 0 } message ? message : null,
            Parameters = parameters,
            Html = kind == ActionKind.Html ? element.InnerHtml.Trim() : "",
            Faults = faults
        };
    }

    /// <remarks>
    /// Values become strings whatever the JSON said, because they arrive at a handler through the
    /// query string and the binding parses them back. A nested object keeps its JSON text, which is
    /// the only lossless answer for a shape the transport cannot represent.
    /// </remarks>
    private static bool ReadParameters(string content, Dictionary<string, string> parameters) {
        if (string.IsNullOrWhiteSpace(content)) {
            // No parameters is not a fault. An action that only names a route sends {} or nothing.
            return true;
        }

        try {
            using var document = JsonDocument.Parse(content);

            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                return false;
            }

            foreach (var value in document.RootElement.EnumerateObject()) {
                parameters[value.Name] = value.Value.ValueKind switch {
                    JsonValueKind.String => value.Value.GetString()!,
                    JsonValueKind.Null => "",
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.Value.GetRawText()
                };
            }

            return true;
        }
        catch (JsonException) {
            return false;
        }
    }

    private static string? Attribute(IElement element, string name) =>
        element.GetAttribute(name)?.Trim();
}
