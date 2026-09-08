namespace LambdaWidgets.Dashboard;

/// <summary>
/// What a widget looks like once the console has dealt with it.
/// </summary>
/// <param name="Kind">Whether the function answered with HTML, markdown or JSON.</param>
/// <param name="Html">What the console renders, after the sanitizer.</param>
/// <param name="Styles">The console's default styling for the dashboard's theme, or empty when the widget opted out.</param>
/// <param name="Actions">Every <c>cwdb-action</c> in the response, in document order.</param>
/// <param name="Forms">The widget's named fields and the values the function rendered them with.</param>
/// <param name="Removals">What the sanitizer took out, which the console does silently.</param>
public sealed record ShownWidget(
    ResponseKind Kind,
    string Html,
    string Styles,
    IReadOnlyList<WidgetAction> Actions,
    IReadOnlyDictionary<string, string> Forms,
    IReadOnlyList<Removal> Removals);

/// <summary>
/// The CloudWatch console's side of a custom widget: what it sends, and what it shows.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, resolved from the container by both consumers. The harness renders a widget
/// in a browser and the test driver clicks through one with no browser at all, and if each worked
/// out for itself what a click sends they would drift — a driver test would then pass against
/// behaviour the harness never reproduces, and neither would match the console. That is the whole
/// reason this is a library.
/// </para>
/// <para>
/// The methods are named for what the user did, because that is what a caller knows. A harness
/// knows the viewer clicked something; it does not know that answering that means parsing actions,
/// collecting form fields and merging three sources of parameters in a particular order.
/// </para>
/// </remarks>
public interface IWidgetConsole {
    /// <summary>
    /// The invocation for a widget appearing on the dashboard.
    /// </summary>
    WidgetEvent Opens(DashboardWidget widget, DashboardState state);

    /// <summary>
    /// The invocation for a refresh, a resize, or the dashboard's time range changing.
    /// </summary>
    /// <remarks>
    /// <b>The same event as <see cref="Opens"/>, deliberately.</b> The console re-invokes with the
    /// widget's <em>configured</em> parameters, so a widget the viewer navigated three pages into
    /// returns to its landing page whenever the dashboard refreshes. This is separate from
    /// <see cref="Opens"/> only so that a caller reads what it meant and a reader of this interface
    /// learns the behaviour; a widget has to be designed for it.
    /// </remarks>
    WidgetEvent Refreshes(DashboardWidget widget, DashboardState state);

    /// <summary>
    /// The invocation behind the console's <em>Get documentation</em> button.
    /// </summary>
    WidgetEvent AsksForDocumentation(DashboardWidget widget, DashboardState state);

    /// <summary>
    /// What the console displays, given the body the Invoke API returned.
    /// </summary>
    ShownWidget Shows(string invokeResponse, DashboardState state);

    /// <summary>
    /// The invocation for a viewer firing one of a shown widget's actions.
    /// </summary>
    /// <param name="shown">What is on screen, which is where the action and the form fields come from.</param>
    /// <param name="action">Its index among <see cref="ShownWidget.Actions"/>.</param>
    /// <param name="edits">Fields the viewer changed. Anything untouched keeps what the function rendered.</param>
    /// <exception cref="ArgumentOutOfRangeException">No action has that index.</exception>
    /// <exception cref="InvalidOperationException">
    /// The action displays HTML rather than calling a function, so there is no invocation to make.
    /// A caller checks <see cref="WidgetAction.Kind"/> first; the harness shows the content instead.
    /// </exception>
    WidgetEvent Clicks(
        ShownWidget shown,
        int action,
        IReadOnlyDictionary<string, string> edits,
        DashboardWidget widget,
        DashboardState state);
}
