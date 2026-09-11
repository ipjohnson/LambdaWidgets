namespace LambdaWidgets.Dashboard;

/// <inheritdoc />
/// <remarks>
/// Composition and nothing else. Every rule lives in one of the services below, and this decides
/// which of them a viewer's action needs and in what order — which is the part a harness would
/// otherwise have to know, and get subtly different from the way a test driver knew it.
/// </remarks>
public sealed class WidgetConsole : IWidgetConsole {
    private readonly IWidgetActions _actions;
    private readonly IWidgetForms _forms;
    private readonly IWidgetSanitizer _sanitizer;
    private readonly IWidgetStyles _styles;
    private readonly IWidgetResponses _responses;
    private readonly IWidgetEvents _events;
    private readonly IWidgetLinter _linter;

    public WidgetConsole(
        IWidgetActions actions,
        IWidgetForms forms,
        IWidgetSanitizer sanitizer,
        IWidgetStyles styles,
        IWidgetResponses responses,
        IWidgetEvents events,
        IWidgetLinter linter) {
        _actions = actions;
        _forms = forms;
        _sanitizer = sanitizer;
        _styles = styles;
        _responses = responses;
        _events = events;
        _linter = linter;
    }

    public WidgetEvent Opens(DashboardWidget widget, DashboardState state) =>
        _events.Initial(widget, state);

    /// <remarks>
    /// The same call as <see cref="Opens"/>. The duplication is the behaviour: the console does not
    /// remember where a widget had navigated to.
    /// </remarks>
    public WidgetEvent Refreshes(DashboardWidget widget, DashboardState state) =>
        _events.Initial(widget, state);

    public WidgetEvent AsksForDocumentation(DashboardWidget widget, DashboardState state) =>
        _events.Describe(widget, state);

    public ShownWidget Shows(string invokeResponse, DashboardState state) {
        var response = _responses.Classify(invokeResponse);

        // Only an HTML answer has actions, fields or anything to strip. Markdown and JSON are
        // displayed as text, so parsing them as a document would invent both.
        if (response.Kind != ResponseKind.Html) {
            return new ShownWidget(
                response.Kind,
                response.Content,
                Styles: "",
                Actions: Array.Empty<WidgetAction>(),
                Forms: new Dictionary<string, string>(),
                Removals: Array.Empty<Removal>(),
                // Markdown and JSON are text. Linting them would report the markup inside a code
                // block as a widget's mistake — but an object where a page was expected is itself
                // worth one, because the console displays it and a test otherwise sees a widget
                // that rendered nothing and found nothing wrong.
                Findings: NotAPage(response),
                Failed: response.Kind == ResponseKind.Json);
        }

        var cleaned = _sanitizer.Clean(response.Content);

        // Linted from the raw answer rather than the cleaned one, because half the findings are
        // about what the cleaning removed and there is nothing left of those afterwards.
        var findings = _linter.Lint(response.Content);

        // Read from the cleaned HTML rather than the raw. An action inside a stripped element is
        // not on screen, so a driver must not be able to click it.
        return new ShownWidget(
            response.Kind,
            cleaned.Html,
            _styles.For(state.Theme, cleaned.Html),
            _actions.In(cleaned.Html),
            _forms.In(cleaned.Html),
            cleaned.Removals,
            findings,
            findings.Any(finding => finding.Rule == WidgetLinter.InvocationFailed));
    }

    /// <remarks>
    /// Markdown is a documented answer and gets none of these. A JSON object is what a function
    /// returning the wrong shape produces, and it is the answer a thrown handler used to give.
    /// </remarks>
    private static IReadOnlyList<Finding> NotAPage(WidgetResponse response) =>
        response.Kind == ResponseKind.Json
            ? [new Finding(
                WidgetLinter.NotAPage,
                "the function answered with a JSON object rather than with HTML or markdown. The " +
                "console displays it as text, so the widget renders and shows no page.",
                response.Content.Length > 120 ? response.Content[..120] + "…" : response.Content)]
            : Array.Empty<Finding>();

    public WidgetEvent Clicks(
        ShownWidget shown,
        int action,
        IReadOnlyDictionary<string, string> edits,
        DashboardWidget widget,
        DashboardState state) {
        if (action < 0 || action >= shown.Actions.Count) {
            throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                $"This widget has {shown.Actions.Count} action(s), so there is nothing at that index.");
        }

        var fired = shown.Actions[action];

        if (fired.Kind != ActionKind.Call) {
            throw new InvalidOperationException(
                $"Action {action} displays HTML rather than calling a function, so there is no " +
                "invocation to make. Read its Html and show that instead.");
        }

        return _events.ForAction(fired, _forms.In(shown.Html, edits), widget, state);
    }
}
