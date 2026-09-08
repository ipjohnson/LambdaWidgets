namespace LambdaWidgets.Charts;

/// <summary>What a chart is being asked to show.</summary>
public enum ChartForm {
    /// <summary>Change over time. One series gets an area wash under it; several do not.</summary>
    Line,

    /// <summary>Magnitude across named things, as columns from a single baseline.</summary>
    Columns,

    /// <summary>One number, because a one-bar bar chart is a number wearing a costume.</summary>
    Stat
}

/// <summary>
/// A chart, described rather than drawn.
/// </summary>
/// <remarks>
/// <para>
/// Built and then handed to the renderer, so what a template writes is what the chart <em>is</em>
/// rather than how it is drawn. That also keeps the decisions checkable: whether a form suits the
/// data is a property of this object, and a test can assert on it without parsing SVG.
/// </para>
/// <para>
/// <b>The form is chosen for you where the data settles it.</b> One category is a stat tile, not a
/// one-bar bar chart. That rule is here rather than in a style guide because a widget author
/// reaching for a chart is usually reaching for the wrong one, and the library is the last place
/// that can say so.
/// </para>
/// </remarks>
public sealed record Chart {
    private Chart() { }

    public required ChartForm Form { get; init; }

    public string Title { get; init; } = "";

    /// <summary>What the values are, for the axis and the table's header.</summary>
    public string Unit { get; init; } = "";

    public IReadOnlyList<Series> Series { get; init; } = [];

    public IReadOnlyList<Category> Categories { get; init; } = [];

    /// <summary>
    /// A route each mark links to, so clicking a column drills into it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optional, and the chart reads correctly without it. Whether the console binds a
    /// <c>cwdb-action</c> to an element inside an <c>svg</c> is unverified — our own interpreter
    /// does, and the probe is what settles the console. So the marks carry actions when a route is
    /// given, and the table beneath the chart carries the same links either way. If the console
    /// turns out not to bind inside SVG, the drill-down is still there.
    /// </para>
    /// </remarks>
    public string? DrillRoute { get; init; }

    /// <summary>The field a drill-down sends, naming which mark was clicked.</summary>
    public string DrillField { get; init; } = "at";

    /// <summary>
    /// Change over time.
    /// </summary>
    /// <remarks>
    /// Anything past three series is folded into "Other" — the cap the palette validates at, and as
    /// many lines as a widget can carry.
    /// </remarks>
    public static Chart Over(string title, params Series[] series) => new() {
        Form = ChartForm.Line,
        Title = title,
        Series = ChartLimits.Fold(series)
    };

    /// <summary>
    /// Magnitude across named things.
    /// </summary>
    /// <remarks>
    /// <b>One category is a stat tile.</b> A single column carries no comparison, so the number
    /// itself is the clearer answer and this returns one.
    /// </remarks>
    public static Chart Across(string title, IReadOnlyList<Category> categories) =>
        categories.Count == 1
            ? Number(title, categories[0].Value)
            : new Chart { Form = ChartForm.Columns, Title = title, Categories = categories };

    /// <summary>One number, large, with its label under it.</summary>
    public static Chart Number(string title, double value) => new() {
        Form = ChartForm.Stat,
        Title = title,
        Categories = [new Category(title, value)]
    };

    public Chart In(string unit) => this with { Unit = unit };

    /// <summary>Makes each mark, and each row of the table, a link to <paramref name="route"/>.</summary>
    /// <param name="route">A string from the generated <c>Links</c>, never a literal.</param>
    /// <param name="field">The parameter naming which mark was clicked.</param>
    public Chart DrillingTo(string route, string field = "at") =>
        this with { DrillRoute = route, DrillField = field };

    /// <summary>Whether there is anything to draw.</summary>
    public bool IsEmpty =>
        Form == ChartForm.Columns ? Categories.Count == 0
        : Form == ChartForm.Stat ? Categories.Count == 0
        : Series.Count == 0 || Series.All(one => one.Readings.Count == 0);
}
