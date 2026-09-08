using System.Xml.Linq;
using Xunit;

namespace Graph.Tests;

/// <summary>
/// A rendered chart, read as a browser reads it.
/// </summary>
/// <remarks>
/// The drawings are pulled out of the widget's HTML and parsed as XML, so an assertion is about
/// elements rather than about substrings. Parsing at all is worth something on its own: an SVG that
/// does not parse is one a browser draws differently from the string the renderer built.
/// </remarks>
internal sealed class Drawing {
    private readonly XElement _svg;

    private Drawing(XElement svg) => _svg = svg;

    /// <summary>
    /// The drawing under a caption.
    /// </summary>
    /// <remarks>
    /// By caption rather than by position, because a test that counts figures breaks when a page
    /// gains a stat tile - which has no drawing at all.
    /// </remarks>
    public static Drawing Titled(string html, string caption) {
        var at = html.IndexOf(">" + caption, StringComparison.Ordinal);

        Assert.True(at > 0, $"No chart is captioned '{caption}'.");

        var start = html.IndexOf("<svg", at, StringComparison.Ordinal);
        var end = html.IndexOf("</svg>", start, StringComparison.Ordinal);

        return new Drawing(XElement.Parse(html[start..(end + 6)]));
    }

    /// <summary>Every drawing on the page.</summary>
    public static IReadOnlyList<Drawing> In(string html) {
        var drawings = new List<Drawing>();

        for (var at = html.IndexOf("<svg", StringComparison.Ordinal); at >= 0;
             at = html.IndexOf("<svg", at + 1, StringComparison.Ordinal)) {
            var end = html.IndexOf("</svg>", at, StringComparison.Ordinal);

            drawings.Add(new Drawing(XElement.Parse(html[at..(end + 6)])));
        }

        return drawings;
    }

    /// <summary>The hit bands: one per moment on a line, one per category on columns.</summary>
    public IReadOnlyList<XElement> Bands =>
        _svg.Descendants().Where(one => (string?)one.Attribute("class") == "lwb").ToList();

    /// <summary>What one band reveals.</summary>
    public static XElement Readout(XElement band) =>
        band.Descendants().First(one => (string?)one.Attribute("class") == "lwr");

    /// <summary>The text a band's readout shows, in the order it is written.</summary>
    public static IReadOnlyList<string> Says(XElement band) =>
        Readout(band).Descendants().Where(one => one.Name.LocalName == "text")
            .Select(one => one.Value).ToList();

    public IEnumerable<string> Text =>
        _svg.Descendants().Where(one => one.Name.LocalName == "text").Select(one => one.Value);
}
