using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// Something the console removed before rendering.
/// </summary>
/// <remarks>
/// Reported rather than only done, because the console removes silently. A widget author whose
/// button does nothing has no way to learn that an <c>onclick</c> was stripped; the harness's
/// inspector shows these, and that is most of why the sanitizer is a library rather than a regex in
/// the page.
/// </remarks>
public sealed record Removal(RemovalKind Kind, string What) {
    public override string ToString() => Kind switch {
        RemovalKind.Element => $"<{What}> removed",
        RemovalKind.EventHandler => $"{What} attribute removed",
        RemovalKind.JavaScriptUrl => $"javascript: URL in {What} removed",
        _ => What
    };
}

public enum RemovalKind {
    Element,
    EventHandler,
    JavaScriptUrl
}

/// <summary>
/// What the console renders, out of what a function returned.
/// </summary>
/// <remarks>
/// <para>
/// The stated reason for all of it is privilege: the function's author must not run code with the
/// viewer's console permissions. That is worth keeping in mind while reading the list, because it
/// says what will never be allowed however useful it would be.
/// </para>
/// <para>
/// <b>The list is the documented one and no more.</b> Whatever else the console strips is
/// unverified until day-one check 3 feeds HTML through AWS's Echo widget and reads back what
/// survived. Guessing wider would make the harness reject what the console accepts, which is the
/// more expensive way to be wrong: an author would work around a restriction that is not real.
/// </para>
/// </remarks>
public interface IWidgetSanitizer {
    /// <summary>Cleans <paramref name="html"/> the way the console does, and says what it took.</summary>
    SanitizedHtml Clean(string html);
}

/// <summary>The result of cleaning: the HTML to render, and what came out of it.</summary>
public sealed record SanitizedHtml(string Html, IReadOnlyList<Removal> Removals);

/// <inheritdoc />
public sealed class WidgetSanitizer : IWidgetSanitizer {
    /// <summary>Elements the console removes outright, with their content.</summary>
    private static readonly string[] Forbidden = ["script", "iframe", "use"];

    public SanitizedHtml Clean(string html) {
        var document = new HtmlParser().ParseDocument(html);
        var removals = new List<Removal>();

        foreach (var name in Forbidden) {
            // Materialised before removing: QuerySelectorAll is live enough that removing while
            // enumerating skips siblings.
            foreach (var element in document.QuerySelectorAll(name).ToArray()) {
                removals.Add(new Removal(RemovalKind.Element, name));
                element.Remove();
            }
        }

        foreach (var element in document.All.ToArray()) {
            StripHandlers(element, removals);
            StripJavaScriptUrls(element, removals);
        }

        return new SanitizedHtml(document.Body?.InnerHtml ?? "", removals);
    }

    /// <remarks>
    /// Every <c>on*</c> attribute rather than a list of known ones. The list of DOM events grows,
    /// and a sanitizer that knows twelve of them is a sanitizer that misses the thirteenth.
    /// </remarks>
    private static void StripHandlers(IElement element, List<Removal> removals) {
        foreach (var attribute in element.Attributes.ToArray()) {
            if (attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) {
                removals.Add(new Removal(RemovalKind.EventHandler, attribute.Name));
                element.RemoveAttribute(attribute.Name);
            }
        }
    }

    /// <remarks>
    /// The attribute is emptied rather than removed, so an anchor that carried one still renders as
    /// the anchor the author wrote instead of turning into bare text.
    /// </remarks>
    private static void StripJavaScriptUrls(IElement element, List<Removal> removals) {
        foreach (var name in new[] { "href", "src", "action", "formaction" }) {
            var value = element.GetAttribute(name);

            if (value is null) {
                continue;
            }

            if (value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) {
                removals.Add(new Removal(RemovalKind.JavaScriptUrl, name));
                element.SetAttribute(name, "");
            }
        }
    }
}
