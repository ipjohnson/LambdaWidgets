namespace LambdaWidgets.Charts;

/// <summary>One reading, at a moment.</summary>
public readonly record struct Reading(DateTimeOffset At, double Value);

/// <summary>One named value, for data whose x axis is a category rather than a time.</summary>
public readonly record struct Category(string Name, double Value);

/// <summary>
/// One line on a chart.
/// </summary>
/// <remarks>
/// The name is what the legend and the end label say, so it is the series' identity rather than a
/// caption. Colour follows the name's position in the chart, never its rank, so filtering one
/// series out does not repaint the others.
/// </remarks>
public sealed record Series(string Name, IReadOnlyList<Reading> Readings);

/// <summary>
/// What a chart can be asked to draw.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three series, and the fourth is folded into "Other".</b> That is the cap the palette
/// validates all-pairs at in both of the console's themes; a fourth puts yellow beside orange,
/// which readers with normal vision cannot reliably separate. It is also as many lines as a widget
/// this size can carry — the default is 588 by 369, and a legend for eight would take a third of it.
/// </para>
/// <para>
/// A widget has no hover, because the console strips JavaScript. Everything a tooltip would have
/// carried has to be in the picture or in the table beneath it, which is why direct labels and the
/// table are not optional here the way they are on the web.
/// </para>
/// </remarks>
public static class ChartLimits {
    /// <summary>How many series keep their own colour before the rest become "Other".</summary>
    public const int Series = 3;

    /// <summary>What the folded tail is called.</summary>
    public const string Other = "Other";

    /// <summary>
    /// Folds anything past <see cref="Series"/> into one summed series.
    /// </summary>
    /// <remarks>
    /// Summed rather than dropped: a chart that silently omits data is worse than one that groups
    /// it, and the reader can see the tail is there. Ordered by total first, so the three that keep
    /// their identity are the three worth looking at.
    /// </remarks>
    public static IReadOnlyList<Series> Fold(IReadOnlyList<Series> series) {
        if (series.Count <= Series) {
            return series;
        }

        var ranked = series.OrderByDescending(one => one.Readings.Sum(reading => reading.Value)).ToList();

        var kept = ranked.Take(Series).ToList();

        var tail = ranked.Skip(Series)
            .SelectMany(one => one.Readings)
            .GroupBy(reading => reading.At)
            .OrderBy(group => group.Key)
            .Select(group => new Reading(group.Key, group.Sum(reading => reading.Value)))
            .ToList();

        kept.Add(new Series(Other, tail));

        return kept;
    }
}
