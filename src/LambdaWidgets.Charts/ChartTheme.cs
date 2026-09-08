namespace LambdaWidgets.Charts;

/// <summary>
/// The colours a chart draws with, resolved for the dashboard's theme.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolved on the server, not in CSS.</b> A web page guesses the reader's theme from
/// <c>prefers-color-scheme</c> and a toggle. A widget does not have to: the console sends its own
/// theme in <c>widgetContext</c>, so the right colours can simply be written into the SVG. No media
/// query, no second definition to drift.
/// </para>
/// <para>
/// <b>The series hues are validated, not chosen by eye.</b> Four slots against the console's own
/// surfaces — white in light, <c>#16191f</c> in dark — pass the lightness band, the chroma floor,
/// colour-vision separation and the normal-vision floor in both. The one warning is aqua on the
/// light surface at 2.74:1, which the rules answer with "visible labels or a table view"; this
/// renderer always emits the table, so the relief is structural rather than remembered.
/// </para>
/// </remarks>
public sealed record ChartTheme {
    /// <summary>What the console renders a light widget on.</summary>
    public static ChartTheme Light { get; } = new() {
        Surface = "#ffffff",
        TextPrimary = "#16191f",
        TextSecondary = "#545b64",
        Grid = "#eaeded",
        Axis = "#aab7b8",
        Series = ["#2a78d6", "#eb6834", "#1baf7a", "#9d4edd"],
        Muted = "#879596"
    };

    /// <summary>The same four hues, stepped for the dark surface rather than inverted.</summary>
    public static ChartTheme Dark { get; } = new() {
        Surface = "#16191f",
        TextPrimary = "#d5dbdb",
        TextSecondary = "#95a5a6",
        Grid = "#2a2e33",
        Axis = "#545b64",
        Series = ["#3987e5", "#d95926", "#199e70", "#9d4edd"],
        Muted = "#687078"
    };

    public required string Surface { get; init; }

    public required string TextPrimary { get; init; }

    public required string TextSecondary { get; init; }

    /// <summary>One step off the surface. Gridlines are hairline and solid, never dashed.</summary>
    public required string Grid { get; init; }

    public required string Axis { get; init; }

    /// <summary>
    /// The validated categorical slots, in fixed order.
    /// </summary>
    /// <remarks>
    /// Four: the three a chart keeps identified, plus the one <c>ChartLimits.Fold</c> gives the
    /// summed tail. The purple is the tail's own colour rather than a repeat, which is what makes
    /// "Other" readable as a fourth line instead of a second blue one.
    /// </remarks>
    public required IReadOnlyList<string> Series { get; init; }

    /// <summary>What a de-emphasised series is drawn in, for the emphasis form.</summary>
    public required string Muted { get; init; }

    /// <summary>
    /// The colour for a series' position, which is its identity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By position and never by rank, so a chart that loses a series does not repaint the ones that
    /// remain — a reader who learned that errors are orange keeps that.
    /// </para>
    /// <para>
    /// Indexed rather than wrapped. Cycling would hand two series the same colour and say nothing;
    /// <c>ChartLimits.Fold</c> is what keeps the count in range, and reaching past the end means
    /// something drew series it had not folded.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// More series were drawn than the palette distinguishes, which <c>ChartLimits.Fold</c> exists
    /// to prevent.
    /// </exception>
    public string For(int position) =>
        position >= 0 && position < Series.Count
            ? Series[position]
            : throw new ArgumentOutOfRangeException(
                nameof(position), position,
                $"The palette has {Series.Count} slots. Fold the series with ChartLimits.Fold " +
                "before drawing them, so the tail becomes one summed series rather than a " +
                "repeated colour.");
}
