using System.Text.RegularExpressions;
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
public sealed partial class WidgetLinter : IWidgetLinter {
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

    /// <summary>
    /// The answer is the error page <c>LambdaWidgets.Runtime</c> writes when a handler threw.
    /// </summary>
    /// <remarks>
    /// <b>This is the rule that makes <c>Assert.Empty(page.Findings)</c> mean something.</b> A
    /// failed invocation and a successful one reach the console as the same shape — a body — so
    /// without this a widget whose query was throttled passes the headline assertion in the README,
    /// the guide and all three templates.
    /// </remarks>
    public const string InvocationFailed = "invocation-failed";

    /// <summary>
    /// The function answered with a JSON object rather than with a page.
    /// </summary>
    /// <remarks>
    /// Raised by <see cref="WidgetConsole"/> rather than here, because the answer never reaches the
    /// linter: only HTML is linted. The console displays the object as text, which renders and is
    /// not a widget.
    /// </remarks>
    public const string NotAPage = "not-a-page";

    /// <summary>The attribute the runtime's error page carries, which is what finds it.</summary>
    /// <remarks>
    /// Named here rather than shared with <c>LambdaWidgets.Runtime</c>: that package is what a
    /// widget deploys and this one is what reads its answer, and neither references the other.
    /// Kept in step by <c>WhatTheLinterFindsTests</c>, which writes the attribute the runtime does.
    /// </remarks>
    public const string ErrorMarker = "data-lw-error";

    /// <summary>
    /// A stylesheet rule whose selector can match another widget's content.
    /// </summary>
    /// <remarks>
    /// Advice rather than a defect, and the only rule here that is. Whether the console isolates
    /// each widget's stylesheet is unverified: AWS's own samples write bare <c>td { }</c>, which
    /// only works if it does. This assumes it does not, because an author who ships a widget that
    /// restyles a colleague's on a shared dashboard hears about it from the colleague.
    /// </remarks>
    public const string UnscopedSelector = "unscoped-selector";

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
        findings.AddRange(UnscopedSelectors(html));
        findings.AddRange(Failures(html));

        return findings;
    }

    /// <remarks>
    /// Reported rather than only rendered. The page says what happened to a viewer; this is what
    /// says it to a test, and to the harness's inspector.
    /// </remarks>
    private static IEnumerable<Finding> Failures(string html) {
        var document = new HtmlParser().ParseDocument(html);

        foreach (var element in document.QuerySelectorAll($"[{ErrorMarker}]")) {
            yield return new Finding(
                InvocationFailed,
                $"the invocation failed with {element.GetAttribute(ErrorMarker)} and answered an " +
                "error page rather than the widget. The function's log has the exception; set " +
                "WIDGET_ERROR_DETAIL to put its message on the page.",
                Markup(element));
        }
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
    /// <summary>
    /// Rules that would match outside this widget if the console does not isolate it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A selector is treated as scoped when every one of its compound parts leads with a class or
    /// an id. <c>.rows td</c> is scoped; <c>td</c> and <c>table.rows</c> are not, because both can
    /// match markup the widget did not write.
    /// </para>
    /// <para>
    /// Deliberately coarse. It reads selectors with a regular expression rather than a CSS parser,
    /// skips at-rule preludes, and reports each distinct selector once. A rule that tried to be
    /// exact here would be a CSS parser, and the finding is advice either way.
    /// </para>
    /// </remarks>
    private static IEnumerable<Finding> UnscopedSelectors(string html) {
        var document = new HtmlParser().ParseDocument(html);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sheet in document.QuerySelectorAll("style")) {
            // Comments first: a selector inside one is not a rule, and the body is scanned as text.
            var css = Comments().Replace(sheet.TextContent, " ");

            foreach (Match rule in Selectors().Matches(css)) {
                var prelude = rule.Groups["selector"].Value.Trim();

                // An at-rule's prelude is a condition, not a selector. Its inner rules are matched
                // separately by the same pass, because the body is scanned as text.
                if (prelude.Length == 0 || prelude.StartsWith('@')) {
                    continue;
                }

                foreach (var selector in prelude.Split(',', StringSplitOptions.TrimEntries)) {
                    if (selector.Length == 0 || Scoped(selector) || !seen.Add(selector)) {
                        continue;
                    }

                    yield return new Finding(
                        UnscopedSelector,
                        $"'{selector}' can match other widgets on the same dashboard. Every widget " +
                        "on a dashboard shares one page, and whether the console isolates their " +
                        "stylesheets is unverified. Lead each part with a class of your own.",
                        selector);
                }
            }
        }
    }

    /// <remarks>
    /// Every compound part has to lead with a class or an id. <c>.rows td</c> passes because the
    /// first part confines it; <c>td .cell</c> does not, because <c>td</c> matches on its own.
    /// </remarks>
    private static bool Scoped(string selector) {
        var first = selector.Split([' ', '>', '+', '~'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return first is { Length: > 0 } && (first[0] == '.' || first[0] == '#');
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    /// <summary>A rule's prelude: everything up to the brace that opens its body.</summary>
    [GeneratedRegex(@"(?<selector>[^{}]+)\{", RegexOptions.Singleline)]
    private static partial Regex Selectors();

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
