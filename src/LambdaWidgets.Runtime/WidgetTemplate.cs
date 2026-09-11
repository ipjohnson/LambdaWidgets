using Hardened.Requests.Abstract.Templates;
using Hardened.Templates.RazorBlade;
using Microsoft.Extensions.DependencyInjection;
using RazorBlade;

namespace LambdaWidgets.Runtime;

/// <summary>
/// The base a widget's Razor views inherit, which is <see cref="HardenedHtmlTemplate{TModel}"/>
/// with the <c>cwdb-action</c> helpers attached.
/// </summary>
/// <remarks>
/// <para>
/// A view does not inherit this directly. It inherits the base the generator emits from
/// <see cref="WidgetTemplates"/>, which derives from this and adds the application's <c>Links</c>.
/// </para>
/// <para>
/// <b>A page, which is not the same thing as a partial.</b> This base is a response output: the
/// pipeline constructs it and attaches the handler's model, so there is no constructor a page can
/// call. Shared chrome inherits <see cref="WidgetPartial{TModel,TLinks}"/> instead.
/// </para>
/// </remarks>
public abstract class WidgetTemplate<TModel> : HardenedHtmlTemplate<TModel> {
    private WidgetHelpers? _widget;

    /// <summary>The helpers, which write the console's interaction element.</summary>
    /// <remarks>
    /// Built on first use rather than injected, because a template is constructed before its
    /// context is attached and there is nothing to read until then.
    /// </remarks>
    protected WidgetHelpers Widget =>
        _widget ??= new WidgetHelpers(
            Context.RequestServices.GetRequiredService<IWidgetContext>());
}

/// <summary>
/// Writes the console's <c>cwdb-action</c> elements.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every route argument comes from the generated <c>Links</c> or <c>Routes</c> type, never from
/// a literal.</b> That is the guarantee the whole design is for: a renamed handler breaks the
/// template at its own line during the build, rather than producing a widget whose button silently
/// does nothing. Nothing here can enforce it — a string is a string — so it is the one rule a
/// reviewer has to hold.
/// </para>
/// <para>
/// Fields are name and value pairs written by hand rather than serialized from an object. A widget
/// carries everything as text and the binding parses it back, so there is no type to serialize and
/// nothing for the ahead-of-time analyzer to object to.
/// </para>
/// </remarks>
public sealed class WidgetHelpers {
    private readonly IWidgetContext _context;

    /// <summary>
    /// A template reads <c>Widget</c> rather than constructing one. This is for a handler that
    /// builds a fragment of markup itself.
    /// </summary>
    /// <remarks>
    /// Public because internal left the helpers reachable only from a template, so a widget that
    /// wanted one row of shared markup in a handler had to write the <c>cwdb-action</c> by hand —
    /// which is the one thing the helpers exist to stop anybody doing.
    /// </remarks>
    public WidgetHelpers(IWidgetContext context) => _context = context;

    /// <summary>
    /// The dashboard as it reached this widget: theme, time range, period, and the widget's own
    /// parameters.
    /// </summary>
    /// <remarks>
    /// A view needs this most often for the theme. The console wraps a widget in a container
    /// carrying its own theme class, so a widget writes no wrapper and no theme class of its own;
    /// a view that wants different colours reads them from here and writes them in, the way the
    /// charts do.
    /// </remarks>
    public IWidgetContext Context => _context;

    /// <summary>
    /// An anchor bound to a call: click it and the widget re-invokes at <paramref name="route"/>.
    /// </summary>
    /// <param name="text">What the viewer reads.</param>
    /// <param name="route">A string from the generated <c>Links</c>, never a literal.</param>
    /// <param name="fields">Extra parameters to send, such as a paging cursor.</param>
    public IEncodedContent Link(
        string text,
        string route,
        params (string Name, string Value)[] fields) =>
        Call(Anchor(text, css: null), route, fields);

    /// <summary>The same, styled as the console's own button.</summary>
    /// <param name="primary">The orange one, which the console reserves for the main action.</param>
    public IEncodedContent Button(
        string text,
        string route,
        bool primary = false,
        params (string Name, string Value)[] fields) =>
        Call(Anchor(text, primary ? "btn btn-primary" : "btn"), route, fields);

    /// <summary>
    /// A call that asks first. The console shows the message with a cancel, and does nothing if the
    /// viewer declines.
    /// </summary>
    /// <remarks>
    /// The plan's reason for a widget existing at all: a destructive step gets a confirmation the
    /// console enforces, where a script on a laptop gets one if its author remembered.
    /// </remarks>
    public IEncodedContent Confirm(
        string text,
        string confirmation,
        string route,
        bool primary = false,
        params (string Name, string Value)[] fields) =>
        Call(Anchor(text, primary ? "btn btn-primary" : "btn"), route, fields, confirmation);

    /// <summary>
    /// A call whose result opens in a modal rather than replacing the widget.
    /// </summary>
    public IEncodedContent Detail(
        string text,
        string route,
        params (string Name, string Value)[] fields) =>
        Call(Anchor(text, css: null), route, fields, confirmation: null, Display.Popup);

    /// <summary>
    /// The action alone, for an element the template already wrote.
    /// </summary>
    /// <remarks>
    /// <b>It has to come immediately after that element.</b> A <c>cwdb-action</c> binds its previous
    /// sibling, so anything between the two silently breaks the binding — the element renders and
    /// the click does nothing. <see cref="Link"/> and <see cref="Button"/> write both and cannot get
    /// it wrong; this one can, and the harness's linter is what catches it.
    /// </remarks>
    public IEncodedContent Action(
        string route,
        params (string Name, string Value)[] fields) =>
        Call(before: null, route, fields);

    /// <summary>Content shown in a modal when the previous element is clicked.</summary>
    public IEncodedContent Popup(string html) =>
        Html(html, Display.Popup, "click");

    /// <summary>Content shown when the pointer enters the previous element.</summary>
    /// <remarks>The console allows <c>mouseenter</c> only on an <c>html</c> action, never a call.</remarks>
    public IEncodedContent Hover(string html) =>
        Html(html, Display.Popup, "mouseenter");

    private IEncodedContent Call(
        string? before,
        string route,
        (string Name, string Value)[] fields,
        string? confirmation = null,
        Display display = Display.Widget) {
        var action = new System.Text.StringBuilder();

        if (before is not null) {
            action.Append(before);
        }

        action.Append("<cwdb-action action=\"call\" endpoint=\"")
              .Append(Escape(_context.InvokedFunctionArn))
              .Append('"');

        if (display == Display.Popup) {
            action.Append(" display=\"popup\"");
        }

        if (!string.IsNullOrEmpty(confirmation)) {
            action.Append(" confirmation=\"").Append(Escape(confirmation)).Append('"');
        }

        action.Append('>').Append(Parameters(route, fields)).Append("</cwdb-action>");

        return new HtmlString(action.ToString());
    }

    private static IEncodedContent Html(string html, Display display, string on) {
        var action = new System.Text.StringBuilder("<cwdb-action");

        if (display == Display.Popup) {
            action.Append(" display=\"popup\"");
        }

        if (on != "click") {
            action.Append(" event=\"").Append(on).Append('"');
        }

        // The content of an html action is markup the console displays, so it is written through
        // rather than escaped. A template passing viewer input here is writing whatever they typed
        // into the widget, which is the author's decision and the reason this takes a string.
        return new HtmlString(action.Append('>').Append(html).Append("</cwdb-action>").ToString());
    }

    /// <summary>
    /// The call's JSON: the route, then the fields.
    /// </summary>
    /// <remarks>
    /// Written with <c>Utf8JsonWriter</c> rather than assembled, so a value carrying a quote or a
    /// newline cannot break out of the JSON and into the surrounding HTML.
    /// </remarks>
    private static string Parameters(string route, (string Name, string Value)[] fields) {
        var buffer = new MemoryStream();

        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();
            writer.WriteString(WidgetInvocation.RouteField, route);

            foreach (var field in fields) {
                writer.WriteString(field.Name, field.Value);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string Anchor(string text, string? css) =>
        css is null
            ? "<a>" + Escape(text) + "</a>"
            : "<a class=\"" + css + "\">" + Escape(text) + "</a>";

    /// <remarks>
    /// The text a template passes is the author's, but it routinely holds data a widget read out of
    /// a log or a table, so it is escaped rather than trusted.
    /// </remarks>
    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}

/// <summary>
/// Where the result of an action is shown.
/// </summary>
/// <remarks>
/// Internal. <c>LambdaWidgets.Dashboard</c> has a public <c>Display</c> for the same idea, and a
/// widget author needs one of them rather than a choice between two — the helpers pick per method,
/// so nothing outside this assembly has to name it.
/// </remarks>
internal enum Display {
    /// <summary>Replace the widget's content. The console's default.</summary>
    Widget,

    /// <summary>Show the result in a modal.</summary>
    Popup
}

/// <summary>
/// The marker a widget application enables to get a view base with the <c>cwdb-action</c> helpers.
///
/// <code>
/// [HardenedModule]
/// [LambdaWidgetModule]
/// [Enable&lt;WidgetTemplates&gt;]
/// public partial class LogsSearch { }
/// </code>
///
/// <para>
/// The generator then emits <c>LogsSearchWidgetTemplates&lt;TModel&gt;</c>, and a view inherits
/// that:
/// </para>
///
/// <code>
/// @inherits LogsSearchWidgetTemplates&lt;ResultsPage&gt;
/// @Widget.Button("Run query", Links.Pages.Search(), primary: true)
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// <b>The generated base is what carries <c>Links</c>, which is the whole point.</b> A view
/// inheriting <see cref="WidgetTemplate{TModel}"/> directly would get the helpers and no generated
/// links, and every route in it would be a literal string — which is exactly the failure the
/// helpers exist to prevent. The plan's section 5.4 shows the direct inherit; it is wrong, and this
/// is why.
/// </para>
/// <para>
/// It stands beside <c>RazorTemplates</c> rather than replacing it. An application enabling both
/// gets two bases and picks per view, which is what a widget that also serves a plain page wants.
/// </para>
/// </remarks>
[TemplateBase(typeof(WidgetTemplate<>))]
[TemplateContentType("text/html; charset=utf-8")]
public sealed class WidgetTemplates { }
