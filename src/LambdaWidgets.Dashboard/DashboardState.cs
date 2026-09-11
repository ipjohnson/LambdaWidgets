namespace LambdaWidgets.Dashboard;

/// <summary>
/// The dashboard's period, which the console sends as <c>period</c> and <c>isAutoPeriod</c>.
/// </summary>
/// <remarks>
/// Two fields rather than a nullable one, because the console sends a number either way: automatic
/// means the console chose it, not that it is absent.
/// </remarks>
public readonly record struct Period(int Seconds, bool IsAutomatic) {
    /// <summary>The console's own choice, which it still reports as a number of seconds.</summary>
    public static Period Auto(int seconds = 300) => new(seconds, true);

    public static Period Of(int seconds) => new(seconds, false);
}

/// <summary>
/// A timezone as <c>widgetContext.timezone</c> carries it.
/// </summary>
/// <remarks>
/// <paramref name="OffsetIso"/> is the console's own <c>+00:00</c> form rather than a
/// <see cref="TimeSpan"/>, because it is written back into the event verbatim and round-tripping it
/// through a parse is a way to change it.
/// </remarks>
public readonly record struct WidgetTimezone(string Label, string OffsetIso, int OffsetInMinutes) {
    public static readonly WidgetTimezone Utc = new("UTC", "+00:00", 0);
}

/// <summary>
/// The dashboard's time range, in the shape <c>widgetContext.timeRange</c> carries.
/// </summary>
/// <remarks>
/// <para>
/// Times are epoch milliseconds on the wire. They are <see cref="DateTimeOffset"/> here and
/// converted at the edge, so nothing above this has to remember which unit a number is in.
/// </para>
/// <para>
/// <b><see cref="Zoom"/> is what a handler should read, through <see cref="Effective"/>.</b> The
/// console sends it only when the user has zoomed a chart, and the AWS sample takes
/// <c>timeRange.zoom || timeRange</c>. A widget that reads <see cref="Start"/> directly ignores the
/// zoom the user just did.
/// </para>
/// </remarks>
public readonly record struct WidgetTimeRange {
    private WidgetTimeRange(
        TimeRangeMode mode,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan? relativeStart,
        (DateTimeOffset Start, DateTimeOffset End)? zoom) {
        Mode = mode;
        Start = start;
        End = end;
        RelativeStart = relativeStart;
        Zoom = zoom;
    }

    public TimeRangeMode Mode { get; }

    public DateTimeOffset Start { get; }

    public DateTimeOffset End { get; }

    /// <summary>How far back a relative range reaches, which the console sends as milliseconds.</summary>
    public TimeSpan? RelativeStart { get; }

    /// <summary>Present only when the user has zoomed a chart.</summary>
    public (DateTimeOffset Start, DateTimeOffset End)? Zoom { get; }

    /// <summary>The range a widget should query over: the zoom when there is one.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) Effective =>
        Zoom ?? (Start, End);

    /// <summary>The last <paramref name="span"/> ending now.</summary>
    public static WidgetTimeRange Relative(TimeSpan span) =>
        Relative(span, DateTimeOffset.UtcNow);

    /// <summary>The last <paramref name="span"/> ending at <paramref name="end"/>, for a test.</summary>
    public static WidgetTimeRange Relative(TimeSpan span, DateTimeOffset end) =>
        new(TimeRangeMode.Relative, end - span, end, span, null);

    public static WidgetTimeRange Absolute(DateTimeOffset start, DateTimeOffset end) =>
        new(TimeRangeMode.Absolute, start, end, null, null);

    /// <summary>The same range with a chart zoom over it.</summary>
    public WidgetTimeRange ZoomedTo(DateTimeOffset start, DateTimeOffset end) =>
        new(Mode, Start, End, RelativeStart, (start, end));
}

public enum TimeRangeMode {
    Relative,
    Absolute
}

/// <summary>
/// Everything the dashboard contributes to an event that is not the widget's own configuration.
/// </summary>
/// <remarks>
/// The harness's controls write to one of these, and every widget on the page is invoked from it.
/// That is what makes the local dashboard behave like the console's: changing the time range once
/// re-invokes each widget with the same new range, rather than each widget holding its own.
/// </remarks>
public sealed record DashboardState {
    public string Name { get; init; } = "dashboard";

    public string AccountId { get; init; } = "000000000000";

    public string Locale { get; init; } = "en";

    public WidgetTimezone Timezone { get; init; } = WidgetTimezone.Utc;

    public Period Period { get; init; } = Period.Auto();

    /// <remarks>
    /// The same three hours the runtime gives a payload that carries no <c>timeRange</c>. The two
    /// have to agree: a driver that sent a real range while the runtime's default was a zero-length
    /// window at the epoch is a suite that cannot catch the widget rendering nothing.
    /// </remarks>
    public WidgetTimeRange TimeRange { get; init; } = WidgetTimeRange.Relative(TimeSpan.FromHours(3));

    public Theme Theme { get; init; } = Theme.Light;

    /// <summary>The dashboard's "link graphs" toggle, which the console reports as <c>linkCharts</c>.</summary>
    public bool LinkCharts { get; init; }
}
