using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// The widget's form fields, which travel to a function under <c>widgetContext.forms.all</c>.
/// </summary>
/// <remarks>
/// <para>
/// Keyed by the field's <c>name</c>. A field without one never reaches the function, which the
/// linter reports because nothing else does.
/// </para>
/// <para>
/// <b>Whether the console collects every named field in the widget or only those inside a
/// <c>form</c> is unverified</b> until day-one check 3 runs. Every named field is collected here,
/// because that is the behaviour the AWS Logs Insights sample reads as if it had: it wraps its
/// fields in a <c>form</c> and then reads <c>forms.all.logGroups</c> by name rather than by form.
/// If the probe shows the console is stricter, this narrows and the fixtures say so.
/// </para>
/// </remarks>
public interface IWidgetForms {
    /// <summary>Every named field in the document, with the value the function rendered it with.</summary>
    IReadOnlyDictionary<string, string> In(string html);

    /// <summary>
    /// The same, with what the viewer typed written over the rendered defaults.
    /// </summary>
    /// <remarks>
    /// This is what travels with a click: the page reports what is in the fields now, and anything
    /// the viewer did not touch keeps the value the function rendered.
    /// </remarks>
    IReadOnlyDictionary<string, string> In(string html, IReadOnlyDictionary<string, string> edited);
}

/// <inheritdoc />
public sealed class WidgetForms : IWidgetForms {
    public IReadOnlyDictionary<string, string> In(string html) =>
        Collect(new HtmlParser().ParseDocument(html));

    public IReadOnlyDictionary<string, string> In(
        string html, IReadOnlyDictionary<string, string> edited) {
        var values = new Dictionary<string, string>(Collect(new HtmlParser().ParseDocument(html)),
            StringComparer.Ordinal);

        foreach (var value in edited) {
            values[value.Key] = value.Value;
        }

        return values;
    }

    private static Dictionary<string, string> Collect(IDocument document) {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in document.QuerySelectorAll("input[name], textarea[name], select[name]")) {
            var name = field.GetAttribute("name");

            if (string.IsNullOrEmpty(name)) {
                continue;
            }

            if (Value(field) is { } value) {
                values[name] = value;
            }
        }

        return values;
    }

    /// <remarks>
    /// The rendered value rather than the live one, because this reads HTML a function returned
    /// rather than a browser's DOM. For a checkbox or radio that means the <c>checked</c> attribute
    /// decides whether the field is present at all, which is how a browser submits one: an
    /// unchecked box sends nothing rather than sending false.
    /// </remarks>
    private static string? Value(IElement field) => field switch {
        IHtmlInputElement input when IsToggle(input) =>
            input.HasAttribute("checked") ? Checked(input) : null,
        IHtmlInputElement input => input.GetAttribute("value") ?? "",
        IHtmlTextAreaElement area => area.TextContent,
        IHtmlSelectElement select => Selected(select),
        _ => null
    };

    private static bool IsToggle(IHtmlInputElement input) =>
        string.Equals(input.GetAttribute("type"), "checkbox", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.GetAttribute("type"), "radio", StringComparison.OrdinalIgnoreCase);

    /// <summary>A checked box with no value submits "on", as a browser does.</summary>
    private static string Checked(IHtmlInputElement input) =>
        input.GetAttribute("value") ?? "on";

    /// <remarks>
    /// The marked option, or the first one, which is what a browser submits for a select nothing
    /// has been chosen in.
    /// </remarks>
    private static string? Selected(IHtmlSelectElement select) {
        IElement? first = null;

        foreach (var option in select.QuerySelectorAll("option")) {
            first ??= option;

            if (option.HasAttribute("selected")) {
                return OptionValue(option);
            }
        }

        return first is null ? null : OptionValue(first);
    }

    private static string OptionValue(IElement option) =>
        option.GetAttribute("value") ?? option.TextContent.Trim();
}
