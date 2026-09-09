using AngleSharp.Html.Parser;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// The styling the console applies to a widget's HTML before the widget's own CSS.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scoped to <c>.lw-widget</c>, which whoever renders this puts on the element wrapping the
/// widget's HTML.</b> The widget writes no wrapper of its own: the console supplies one, and a
/// widget that emitted a container to be styled against would be emitting something inert in
/// production. Which class the console actually uses is day-one check 3. The one confirmed name is
/// <c>cwdb-theme-dark</c>, which AWS's own costExplorerReport sample selects on as an ancestor.
/// </para>
/// </remarks>
/// <remarks>
/// <para>
/// A widget that writes a bare <c>&lt;table&gt;</c> gets a styled table in the console and an
/// unstyled one anywhere else, which is the single largest source of "it looked right locally".
/// This is the harness's answer to that.
/// </para>
/// <para>
/// <b>Approximate, and unverified.</b> The documented facts are that <c>table</c>, <c>select</c>,
/// <c>h1</c> to <c>h3</c>, <c>pre</c>, <c>input</c> and <c>textarea</c> are styled, that
/// <c>btn</c> and <c>btn btn-primary</c> turn an anchor into a button, and that one element
/// carrying <c>cwdb-no-default-styles</c> turns the whole thing off. The values below are read off
/// the console's appearance rather than out of its stylesheet, and day-one check 3 is what replaces
/// them with measurements. Until then a widget that depends on an exact metric here is depending on
/// a guess.
/// </para>
/// </remarks>
public interface IWidgetStyles {
    /// <summary>
    /// The stylesheet for a theme, or empty when the HTML opts out.
    /// </summary>
    /// <remarks>
    /// Takes the HTML because the opt-out is a property of the document rather than of the
    /// dashboard: a single element anywhere carrying the class turns the defaults off for the whole
    /// widget.
    /// </remarks>
    string For(Theme theme, string html);
}

/// <inheritdoc />
public sealed class WidgetStyles : IWidgetStyles {
    /// <summary>The class that turns the defaults off, wherever it appears.</summary>
    public const string OptOutClass = "cwdb-no-default-styles";

    public string For(Theme theme, string html) =>
        OptsOut(html) ? "" : Default(theme);

    /// <summary>Whether any element in the document carries <see cref="OptOutClass"/>.</summary>
    public static bool OptsOut(string html) =>
        new HtmlParser().ParseDocument(html).QuerySelector("." + OptOutClass) is not null;

    /// <summary>The stylesheet, with no opt-out considered.</summary>
    public static string Default(Theme theme) => theme == Theme.Dark ? Dark : Light;

    private const string Shared = """
        .lw-widget { font-family: "Amazon Ember", "Helvetica Neue", Arial, sans-serif; font-size: 14px; line-height: 1.4; }
        .lw-widget h1 { font-size: 22px; font-weight: 700; margin: 0 0 8px; }
        .lw-widget h2 { font-size: 18px; font-weight: 700; margin: 16px 0 8px; }
        .lw-widget h3 { font-size: 16px; font-weight: 700; margin: 14px 0 6px; }
        .lw-widget table { border-collapse: collapse; width: 100%; font-size: 13px; }
        .lw-widget th, .lw-widget td { text-align: left; padding: 6px 10px; }
        .lw-widget th { font-weight: 700; }
        .lw-widget pre { font-family: Monaco, Menlo, Consolas, monospace; font-size: 12px; padding: 10px; overflow-x: auto; }
        .lw-widget input, .lw-widget textarea, .lw-widget select { font: inherit; padding: 4px 8px; border-radius: 2px; }
        .lw-widget textarea { width: 100%; }
        .lw-widget a { text-decoration: none; }
        .lw-widget a:hover { text-decoration: underline; }
        .lw-widget a.btn { display: inline-block; padding: 4px 14px; border-radius: 2px; cursor: pointer; text-decoration: none; font-weight: 700; }
        .lw-widget a.btn:hover { text-decoration: none; }
        """;

    private const string Light = Shared + """

        .lw-widget { color: #16191f; background: #ffffff; }
        .lw-widget th { border-bottom: 1px solid #aab7b8; }
        .lw-widget td { border-bottom: 1px solid #eaeded; }
        .lw-widget pre { background: #f2f3f3; }
        .lw-widget input, .lw-widget textarea, .lw-widget select { border: 1px solid #aab7b8; background: #ffffff; color: #16191f; }
        .lw-widget a { color: #0073bb; }
        .lw-widget a.btn { border: 1px solid #545b64; color: #545b64; background: transparent; }
        .lw-widget a.btn.btn-primary { border-color: #ec7211; background: #ec7211; color: #ffffff; }
        """;

    private const string Dark = Shared + """

        .lw-widget { color: #d5dbdb; background: #16191f; }
        .lw-widget th { border-bottom: 1px solid #545b64; }
        .lw-widget td { border-bottom: 1px solid #2a2e33; }
        .lw-widget pre { background: #21252c; }
        .lw-widget input, .lw-widget textarea, .lw-widget select { border: 1px solid #545b64; background: #21252c; color: #d5dbdb; }
        .lw-widget a { color: #44b9d6; }
        .lw-widget a.btn { border: 1px solid #879596; color: #d5dbdb; background: transparent; }
        .lw-widget a.btn.btn-primary { border-color: #ec7211; background: #ec7211; color: #ffffff; }
        """;
}
