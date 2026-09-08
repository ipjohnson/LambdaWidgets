using Markdig;
using Markdig.Syntax;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// The console's <em>Get documentation</em> answer, and the one thing it does with it beyond
/// rendering.
/// </summary>
public interface IWidgetDocumentation {
    /// <summary>
    /// The example parameters the console lifts into a widget's editor.
    /// </summary>
    string? Parameters(string markdown);

    /// <summary>The markdown as HTML, for a harness that renders it beside the widget.</summary>
    string ToHtml(string markdown);
}

/// <inheritdoc />
public sealed class WidgetDocumentation : IWidgetDocumentation {
    private const string ParameterLanguage = "yaml";

    /// <summary>
    /// The example parameters the console lifts into a widget's editor.
    /// </summary>
    /// <remarks>
    /// <b>The first fenced <c>yaml</c> block, and only the first.</b> AWS's own describe markdown
    /// puts the widget's parameters in one, and the console offers to paste it into the parameters
    /// field. Later blocks are documentation of other things and are left alone.
    /// </remarks>
    public string? Parameters(string markdown) {
        if (string.IsNullOrWhiteSpace(markdown)) {
            return null;
        }

        var document = Markdown.Parse(markdown);

        foreach (var block in document.Descendants<FencedCodeBlock>()) {
            if (!string.Equals(block.Info, ParameterLanguage, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            return Text(block);
        }

        return null;
    }

    /// <summary>The markdown as HTML, for a harness that renders it beside the widget.</summary>
    /// <remarks>
    /// Rendered here rather than in the page, so the harness needs no markdown library in the
    /// browser and the test driver sees the same output the page does.
    /// </remarks>
    public string ToHtml(string markdown) => Markdown.ToHtml(markdown ?? "");

    /// <remarks>
    /// Markdig models a fenced block as its lines rather than as a string, and the slice each line
    /// carries is the region of the source it came from.
    /// </remarks>
    private static string Text(FencedCodeBlock block) {
        var lines = block.Lines.Lines;
        var text = new List<string>(block.Lines.Count);

        for (var i = 0; i < block.Lines.Count; i++) {
            text.Add(lines[i].Slice.ToString());
        }

        return string.Join("\n", text).TrimEnd();
    }
}
