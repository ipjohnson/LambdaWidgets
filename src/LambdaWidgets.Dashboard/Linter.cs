using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LambdaWidgets.Dashboard;

/// <summary>What the linter found.</summary>
/// <param name="Rule">A stable name, so a finding can be suppressed or counted without matching prose.</param>
/// <param name="Message">What is wrong, in the terms the author wrote it in.</param>
/// <param name="Where">The element it is about, as markup, or empty when it is about the document.</param>
public sealed record Finding(string Rule, string Message, string Where = "") {
    public override string ToString() => $"{Rule}: {Message}";
}

/// <summary>
/// The mistakes the console makes no noise about.
/// </summary>
/// <remarks>
/// <para>
/// Every rule here produces a widget that renders and then does nothing when clicked, which is the
/// worst failure mode a widget has: it looks finished. The console strips silently, ignores an
/// unbound action silently, and drops an unnamed field silently, so nothing tells an author except
/// the absence of the behaviour they expected.
/// </para>
/// <para>
/// The rules are the documented ones and no more. A linter that guessed wider would fail widgets
/// the console accepts, which is the more expensive way to be wrong: an author would work around a
/// restriction that is not real. Day-one check 3 is what widens it.
/// </para>
/// </remarks>
public interface IWidgetLinter {
    /// <summary>Everything wrong with this HTML, in document order.</summary>
    IReadOnlyList<Finding> Lint(string html);
}

/// <inheritdoc />
public sealed class WidgetLinter : IWidgetLinter {
    /// <summary>An element the console removes, with everything in it.</summary>
    public const string RemovedElement = "removed-element";

    /// <summary>An <c>on*</c> attribute, which the console strips before rendering.</summary>
    public const string RemovedHandler = "removed-handler";

    /// <summary>A <c>javascript:</c> URL, which the console empties.</summary>
    public const string RemovedUrl = "removed-url";

    /// <summary>An action with nothing before it to bind.</summary>
    public const string UnboundAction = "unbound-action";

    /// <summary>A call with no endpoint, so there is no function to invoke.</summary>
    public const string CallWithoutEndpoint = "call-without-endpoint";

    /// <summary><c>mouseenter</c> on a call, which the console allows only on an html action.</summary>
    public const string MouseEnterOnCall = "mouseenter-on-call";

    /// <summary>A call whose content is not a JSON object.</summary>
    public const string CallContentIsNotJson = "call-content-not-json";

    /// <summary>A field with no <c>name</c>, which never reaches <c>forms.all</c>.</summary>
    public const string FieldWithoutName = "field-without-name";

    private readonly IWidgetActions _actions;
    private readonly IWidgetSanitizer _sanitizer;

    public WidgetLinter(IWidgetActions actions, IWidgetSanitizer sanitizer) {
        _actions = actions;
        _sanitizer = sanitizer;
    }

    public IReadOnlyList<Finding> Lint(string html) {
        var findings = new List<Finding>();

        foreach (var removal in _sanitizer.Clean(html).Removals) {
            findings.Add(removal.Kind switch {
                RemovalKind.Element => new Finding(
                    RemovedElement,
                    $"<{removal.What}> is removed before rendering, with everything inside it.",
                    removal.What),
                RemovalKind.EventHandler => new Finding(
                    RemovedHandler,
                    $"{removal.What} is stripped, so this element does nothing when clicked. " +
                    "A cwdb-action is how a widget responds to a click.",
                    removal.What),
                _ => new Finding(
                    RemovedUrl,
                    $"a javascript: URL in {removal.What} is emptied before rendering.",
                    removal.What)
            });
        }

        // Read from the raw HTML rather than the sanitized: an action inside a stripped element is
        // already reported as the removal, and reporting its faults as well would be two findings
        // for one mistake.
        foreach (var action in _actions.In(html)) {
            foreach (var fault in action.Faults) {
                findings.Add(Describe(action, fault));
            }
        }

        findings.AddRange(UnnamedFields(html));

        return findings;
    }

    private static Finding Describe(WidgetAction action, ActionFault fault) => fault switch {
        ActionFault.NoBoundElement => new Finding(
            UnboundAction,
            $"action {action.Index} has no element before it, so nothing fires it. A cwdb-action " +
            "binds its previous sibling, and anything between the two breaks the binding.",
            action.Text),

        ActionFault.CallWithoutEndpoint => new Finding(
            CallWithoutEndpoint,
            $"'{action.Text}' calls a function with no endpoint, so clicking it does nothing. " +
            "The endpoint is usually the invoked function's own ARN.",
            action.Text),

        ActionFault.MouseEnterOnCall => new Finding(
            MouseEnterOnCall,
            $"'{action.Text}' asks for mouseenter on a call. The console allows mouseenter only " +
            "on an action that displays HTML.",
            action.Text),

        _ => new Finding(
            CallContentIsNotJson,
            $"'{action.Text}' has content that is not a JSON object, so it sends no parameters - " +
            "including the route, which means the click reaches the landing page.",
            action.Text)
    };

    /// <remarks>
    /// A field with no name never reaches the function, which is invisible: the field renders, the
    /// viewer types in it, the click carries everything except that.
    /// </remarks>
    private static IEnumerable<Finding> UnnamedFields(string html) {
        var document = new HtmlParser().ParseDocument(html);

        foreach (var field in document.QuerySelectorAll("input, textarea, select")) {
            if (!string.IsNullOrEmpty(field.GetAttribute("name"))) {
                continue;
            }

            // A submit or a hidden marker is not a field a widget reads back, and reporting one
            // would train an author to ignore the rule.
            if (string.Equals(field.GetAttribute("type"), "submit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.GetAttribute("type"), "button", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            yield return new Finding(
                FieldWithoutName,
                $"a <{field.LocalName}> has no name, so whatever the viewer types in it never " +
                "reaches the function. Only named fields travel in forms.all.",
                Markup(field));
        }
    }

    private static string Markup(IElement element) =>
        element.OuterHtml.Length > 120 ? element.OuterHtml[..120] + "…" : element.OuterHtml;
}
