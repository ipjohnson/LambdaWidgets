using System.Globalization;
using System.Net;
using System.Text;
using DependencyModules.Runtime.Attributes;

namespace LambdaWidgets.Charts;

/// <summary>Draws a chart as markup the console will render.</summary>
public interface IChartRenderer {
    /// <summary>
    /// The chart, and the table that carries the same numbers.
    /// </summary>
    /// <param name="chart">What to draw.</param>
    /// <param name="theme">The dashboard's theme, which the console sent.</param>
    /// <param name="endpoint">The invoked ARN, for a chart whose marks link somewhere.</param>
    string Render(Chart chart, ChartTheme theme, string endpoint);
}

/// <summary>
/// Inline SVG, sized to the widget and drawn to fixed specs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Server-rendered, because a widget cannot run script.</b> The console strips JavaScript
/// before rendering, so there is no charting library, no canvas and no hover. Everything a tooltip
/// would have carried has to be in the picture or in the table under it. That constraint is why the
/// direct labels and the table below are structural here rather than a nicety.
/// </para>
/// <para>
/// The specs are fixed and not options: columns at most 24px with a 4px rounded cap and a square
/// baseline, 2px lines with round joins, markers of at least 8px carrying a 2px ring in the surface
/// colour, an area wash at a tenth opacity, and hairline solid gridlines one step off the surface.
/// A chart that lets a caller set those is a chart that will be set wrong.
/// </para>
/// </remarks>
[SingletonService(As = typeof(IChartRenderer))]
public sealed class SvgChartRenderer : IChartRenderer {
    /// <summary>
    /// The drawing's own coordinate space, which <c>viewBox</c> scales to whatever width the widget
    /// has. A widget's pixel size is in the event, but it changes when the viewer drags the corner
    /// and the SVG should follow without another invocation.
    /// </summary>
    private const int Width = 640;

    private const int Height = 260;

    private const int Left = 52;
    private const int Right = 16;
    private const int Top = 18;
    private const int Bottom = 34;

    private const int MaxColumn = 24;
    private const int Gap = 2;

    /// <summary>How far apart two direct labels have to be to both be readable.</summary>
    private const int LabelHeight = 13;

    /// <summary>
    /// The row under a line chart's axis where the crosshair writes its values.
    /// </summary>
    /// <remarks>
    /// Reserved whether or not anything is hovering, so the widget does not resize under the
    /// pointer. Twenty pixels of empty space is cheaper than a chart that jumps.
    /// </remarks>
    private const int Strip = 20;

    private static int PlotWidth => Width - Left - Right;
    private static int PlotHeight => Height - Top - Bottom;

    public string Render(Chart chart, ChartTheme theme, string endpoint) {
        if (chart.IsEmpty) {
            return Empty(chart, theme);
        }

        return chart.Form switch {
            ChartForm.Stat => Stat(chart, theme),
            ChartForm.Columns => Columns(chart, theme, endpoint),
            _ => Line(chart, theme, endpoint)
        };
    }

    /// <remarks>
    /// A widget with nothing to show says so in words. An empty chart frame reads as a broken
    /// widget, and a viewer cannot tell the two apart.
    /// </remarks>
    private static string Empty(Chart chart, ChartTheme theme) =>
        $"""<p style="color:{theme.TextSecondary}">{Text(chart.Title)}: nothing in this time range.</p>""";

    /// <summary>
    /// One number, large.
    /// </summary>
    /// <remarks>
    /// What a single value should be. A one-column chart spends a whole plot area on an axis, a
    /// baseline and a gridline to say what the number says on its own.
    /// </remarks>
    private static string Stat(Chart chart, ChartTheme theme) {
        var value = chart.Categories[0].Value;

        return $"""
            <div style="padding:8px 0">
              <div style="font-size:40px;line-height:1.1;font-weight:700;color:{theme.TextPrimary}">{Text(Compact(value))}</div>
              <div style="font-size:13px;color:{theme.TextSecondary}">{Text(chart.Title)}{Unit(chart)}</div>
            </div>
            """;
    }

    private static string Columns(Chart chart, ChartTheme theme, string endpoint) {
        var svg = new StringBuilder();

        var top = Ceiling(chart.Categories.Max(one => one.Value));
        var band = (double)PlotWidth / chart.Categories.Count;
        var thickness = Math.Min(MaxColumn, Math.Max(4, band - Gap * 2));

        Open(svg, chart, theme, Height + Strip);
        Grid(svg, theme, top);

        for (var i = 0; i < chart.Categories.Count; i++) {
            var category = chart.Categories[i];
            var height = top <= 0 ? 0 : category.Value / top * PlotHeight;
            var x = Left + band * i + (band - thickness) / 2;
            var y = Top + PlotHeight - height;

            // One series, one colour. Shading each column by its own value would spend the only
            // free channel restating the height, and nominal categories have no order to encode.
            svg.Append(Mark(
                $"""<path d="{Column(x, y, thickness, height)}" fill="{theme.For(0)}"/>""",
                chart, endpoint, category.Name, category.Name));

            svg.Append(Label(
                x + thickness / 2, y - 6, Compact(category.Value), theme.TextSecondary, "middle"));

            // Minus the gap, so two long names in adjacent bands do not run together. Sized to the
            // room a label actually has rather than to the band, which includes its neighbour's
            // whitespace.
            svg.Append(Label(
                x + thickness / 2, Height - Bottom + 16, Clip(category.Name, band - 10),
                theme.TextSecondary, "middle"));

            // The band is the hit target, not the column. A 24px column in a 114px band is a
            // pinpoint, and the reader is aiming at a category rather than at a rectangle.
            svg.Append($"""
                <g class="lwb">
                <rect x="{N(Left + band * i)}" y="{Top}" width="{N(band)}" height="{PlotHeight}"
                      fill="transparent" tabindex="0"/>
                <g class="lwr" opacity="0">
                <path d="{Column(x - 2, y - 2, thickness + 4, height + 2)}"
                      fill="none" stroke="{theme.For(0)}" stroke-width="1.5"/>
                """);

            // The full name, which is the one thing the axis cannot always show.
            svg.Append(Readout(
                theme, [(theme.For(0), category.Name, Compact(category.Value))], moment: ""));

            svg.Append("</g></g>");
        }

        svg.Append("</svg>");

        return Figure(chart, theme, svg.ToString(), endpoint);
    }

    private static string Line(Chart chart, ChartTheme theme, string endpoint) {
        var svg = new StringBuilder();

        var readings = chart.Series.SelectMany(one => one.Readings).ToList();
        var top = Ceiling(readings.Max(one => one.Value));
        var from = readings.Min(one => one.At);
        var to = readings.Max(one => one.At);
        var span = Math.Max(1, (to - from).TotalSeconds);

        Open(svg, chart, theme, Height + Strip);
        Grid(svg, theme, top);

        double X(DateTimeOffset at) => Left + (at - from).TotalSeconds / span * PlotWidth;
        double Y(double value) => Top + PlotHeight - (top <= 0 ? 0 : value / top * PlotHeight);

        // Where two series end close together the labels would overlap. Nudging them apart detaches
        // each from its own line and reads as noise, so the lower one is dropped instead - the
        // legend names it and the table has its value. Direct labels work because they are sparing.
        var labelled = new List<double>();

        for (var s = 0; s < chart.Series.Count; s++) {
            var series = chart.Series[s];

            if (series.Readings.Count == 0) {
                continue;
            }

            var colour = theme.For(s);
            var points = series.Readings.OrderBy(one => one.At).ToList();
            var path = string.Join(" ", points.Select((one, i) =>
                $"{(i == 0 ? "M" : "L")}{N(X(one.At))},{N(Y(one.Value))}"));

            // A wash under a single series, and none under several: overlapping fills muddy every
            // hue they cross and the lines stop being separable.
            if (chart.Series.Count == 1) {
                svg.Append($"""
                    <path d="{path} L{N(X(points[^1].At))},{N(Top + PlotHeight)} L{N(X(points[0].At))},{N(Top + PlotHeight)} Z"
                          fill="{colour}" fill-opacity="0.1"/>
                    """);
            }

            svg.Append($"""
                <path d="{path}" fill="none" stroke="{colour}" stroke-width="2"
                      stroke-linejoin="round" stroke-linecap="round"/>
                """);

            // The end marker, with a ring in the surface colour so it stays legible where lines
            // cross. A ring rather than a stroke: a stroke would add ink that is not data.
            var last = points[^1];

            svg.Append($"""
                <circle cx="{N(X(last.At))}" cy="{N(Y(last.Value))}" r="4"
                        fill="{colour}" stroke="{theme.Surface}" stroke-width="2"/>
                """);

            // The endpoint only. A number beside every reading is chaos and goes unread; the axis
            // and the table carry the rest.
            var at = Y(last.Value) - 10;

            if (labelled.TrueForAll(placed => Math.Abs(placed - at) >= LabelHeight)) {
                labelled.Add(at);

                svg.Append(Label(X(last.At) - 8, at, Compact(last.Value), theme.TextSecondary, "end"));
            }
        }

        svg.Append(Label(Left, Height - Bottom + 16, Moment(from), theme.TextSecondary, "start"));
        svg.Append(Label(Width - Right, Height - Bottom + 16, Moment(to), theme.TextSecondary, "end"));

        Crosshair(svg, chart, theme, X, Y);

        svg.Append("</svg>");

        return Figure(chart, theme, svg.ToString(), endpoint);
    }

    /// <remarks>
    /// <para>
    /// <b>It shrinks to fit and never grows.</b> A <c>viewBox</c> with <c>width="100%"</c> scales
    /// everything, text included: in a widget twice this wide the 11px labels render at 22 and the
    /// chart reads as though somebody zoomed it. Sizing to the design width with
    /// <c>max-width:100%</c> keeps type at the size it was chosen at, and a narrow widget still
    /// scales down rather than clipping.
    /// </para>
    /// <para>
    /// The alternative is to draw at the widget's own pixel size, which <c>widgetContext</c> carries.
    /// That waits on the probe: what the console sends there, and how it relates to the width and
    /// height in a dashboard body, is unverified. See FINDINGS.md day-one check 3.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A hairline and a readout of every series, at the moment the pointer is nearest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No script, because the console strips it.</b> This is CSS <c>:hover</c> and
    /// <c>:focus-within</c> over one transparent band per time bin — which is not JavaScript and
    /// survives the sanitizer. A reader aims at a moment rather than at a 2px line, and gets every
    /// series at that moment rather than the one they managed to land on.
    /// </para>
    /// <para>
    /// <b>Hidden by attribute, revealed by stylesheet.</b> Each readout carries <c>opacity="0"</c>
    /// and the rule only turns it on. If a <c>style</c> block is ever dropped — the console's
    /// handling of them is documented but unverified — every readout stays hidden and the chart is
    /// the static one. The other way round, a rule that hid them, would show all thirty at once.
    /// </para>
    /// <para>
    /// Nothing here gates a value. The table under the chart has every number without hovering, and
    /// the band is focusable so a keyboard reaches the same readout.
    /// </para>
    /// </remarks>
    private static void Crosshair(
        StringBuilder svg, Chart chart, ChartTheme theme, Func<DateTimeOffset, double> x,
        Func<double, double> y) {
        var moments = chart.Series
            .SelectMany(one => one.Readings.Select(reading => reading.At))
            .Distinct()
            .OrderBy(at => at)
            .ToList();

        if (moments.Count < 2) {
            return;
        }

        var band = (double)PlotWidth / moments.Count;

        for (var i = 0; i < moments.Count; i++) {
            var at = moments[i];
            var centre = x(at);

            var rows = chart.Series
                .Select((series, slot) => (
                    Slot: slot,
                    series.Name,
                    Reading: series.Readings.FirstOrDefault(one => one.At == at)))
                .Where(row => row.Reading.At == at)
                .ToList();

            if (rows.Count == 0) {
                continue;
            }

            svg.Append("<g class=\"lwb\">");

            // transparent rather than none: fill:none is not hit-tested, so the band would catch
            // nothing. The band is the hit target and it is the full height of the plot, which is
            // far bigger than any mark.
            svg.Append($"""
                <rect x="{N(Left + band * i)}" y="{Top}" width="{N(band)}" height="{PlotHeight}"
                      fill="transparent" tabindex="0"/>
                """);

            svg.Append("<g class=\"lwr\" opacity=\"0\">");

            svg.Append($"""
                <line x1="{N(centre)}" y1="{Top}" x2="{N(centre)}" y2="{Top + PlotHeight}"
                      stroke="{theme.Axis}" stroke-width="1"/>
                """);

            foreach (var row in rows) {
                svg.Append($"""
                    <circle cx="{N(centre)}" cy="{N(y(row.Reading.Value))}" r="4"
                            fill="{theme.For(row.Slot)}" stroke="{theme.Surface}" stroke-width="2"/>
                    """);
            }

            svg.Append(Readout(theme, rows.Select(row =>
                (theme.For(row.Slot), row.Name, Compact(row.Reading.Value))).ToList(), Moment(at)));

            svg.Append("</g></g>");
        }
    }

    /// <summary>
    /// One row under the axis: the moment, then every series with its value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Under the plot rather than floating over it.</b> A card that follows the pointer covers
    /// the lines on either side of the moment it describes, which is the comparison the reader
    /// came for. This row sits in reserved space below the axis, so it hides nothing and lands in
    /// the same place every time — the eye learns one spot instead of chasing a box.
    /// </para>
    /// <para>
    /// The value leads and the name follows, which is the legend's hierarchy inverted: a reader
    /// looking here already knows the series and wants the number. Each is keyed by a short stroke
    /// in the series colour rather than a filled box, because at this size a box is data-weight ink
    /// doing a label's job, and the text itself stays in text tokens.
    /// </para>
    /// </remarks>
    private static string Readout(
        ChartTheme theme, IReadOnlyList<(string Colour, string Name, string Value)> rows, string moment) {
        var y = Height + 13;
        var x = (double)Left;

        var row = new StringBuilder();

        if (moment.Length > 0) {
            row.Append(Label(x, y, moment, theme.TextSecondary, "start", size: 10));

            x += moment.Length * 5.4 + 14;
        }

        foreach (var (colour, name, value) in rows) {
            // Whatever room is left on the row, so a single-series readout can spell out a name the
            // axis had to truncate - which is most of why the readout is worth having on columns.
            var label = Clip(name, (Width - Right - x - 22 - value.Length * 6.2) / rows.Count);

            row.Append($"""
                <line x1="{N(x)}" y1="{y - 4}" x2="{N(x + 10)}" y2="{y - 4}"
                      stroke="{colour}" stroke-width="2" stroke-linecap="round"/>
                """);

            x += 16;

            row.Append(Label(x, y, value, theme.TextPrimary, "start", size: 11, bold: true));

            x += value.Length * 6.2 + 6;

            row.Append(Label(x, y, label, theme.TextSecondary, "start", size: 11));

            x += label.Length * 5.6 + 16;
        }

        return row.ToString();
    }

    private static void Open(StringBuilder svg, Chart chart, ChartTheme theme, int height = Height) =>
        svg.Append($"""
            <svg viewBox="0 0 {Width} {height}" width="{Width}" role="img"
                 aria-label="{Text(chart.Title)}" style="display:block;max-width:100%;height:auto">
            """);

    /// <remarks>
    /// Hairline, solid and one step off the surface. Dashed reads as a threshold or a projection
    /// when it is only a grid.
    /// </remarks>
    private static void Grid(StringBuilder svg, ChartTheme theme, double top) {
        foreach (var value in Ticks(top)) {
            var y = Top + PlotHeight - (top <= 0 ? 0 : value / top * PlotHeight);

            svg.Append($"""
                <line x1="{Left}" y1="{N(y)}" x2="{Width - Right}" y2="{N(y)}"
                      stroke="{theme.Grid}" stroke-width="1"/>
                """);

            svg.Append(Label(Left - 8, y + 4, Comma(value), theme.TextSecondary, "end"));
        }

        svg.Append($"""
            <line x1="{Left}" y1="{Top + PlotHeight}" x2="{Width - Right}" y2="{Top + PlotHeight}"
                  stroke="{theme.Axis}" stroke-width="1"/>
            """);
    }

    /// <summary>A column: square where it meets the baseline, 4px rounded at the data end.</summary>
    private static string Column(double x, double y, double width, double height) {
        var radius = Math.Min(4, Math.Max(0, height));
        var baseline = y + height;

        return height <= 0
            ? $"M{N(x)},{N(baseline)} h{N(width)}"
            : $"M{N(x)},{N(baseline)} L{N(x)},{N(y + radius)} Q{N(x)},{N(y)} {N(x + radius)},{N(y)} " +
              $"L{N(x + width - radius)},{N(y)} Q{N(x + width)},{N(y)} {N(x + width)},{N(y + radius)} " +
              $"L{N(x + width)},{N(baseline)} Z";
    }

    /// <summary>
    /// A mark, with a <c>cwdb-action</c> after it when the chart drills somewhere.
    /// </summary>
    /// <remarks>
    /// The action is the mark's next sibling inside a <c>g</c>, which is what the console's binding
    /// rule asks for. Our interpreter resolves it; whether the console walks into the SVG namespace
    /// to find it is the open question, which is why the table below carries the same links.
    /// </remarks>
    private static string Mark(string shape, Chart chart, string endpoint, string value, string label) {
        if (chart.DrillRoute is null) {
            return shape;
        }

        return $"""
            <g><a role="link" aria-label="{Text(label)}" style="cursor:pointer">{shape}</a>
            <cwdb-action action="call" endpoint="{Text(endpoint)}">{Action(chart, value)}</cwdb-action></g>
            """;
    }

    private static string Action(Chart chart, string value) =>
        $$"""{"route":{{Quote(chart.DrillRoute!)}},{{Quote(chart.DrillField)}}:{{Quote(value)}}}""";

    /// <summary>
    /// The chart, and under it the same numbers as a table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Always, not on request.</b> It is the identity channel for a reader who cannot separate
    /// two hues, the relief the light-mode contrast check requires, the only way to read an exact
    /// value where a widget has no hover, and the drill-down that works whatever the console does
    /// with actions inside an SVG. Four reasons, and any one of them would be enough.
    /// </para>
    /// </remarks>
    private static string Figure(Chart chart, ChartTheme theme, string svg, string endpoint) {
        var figure = new StringBuilder();

        // Scoped to this figure. Two charts on one dashboard would otherwise light each other up,
        // and a widget's CSS shares a page with every other widget's.
        var scope = "lw" + Math.Abs(chart.Title.GetHashCode()).ToString(CultureInfo.InvariantCulture);

        figure.Append($$"""
            <style>
            .{{scope}} .lwb:hover .lwr, .{{scope}} .lwb:focus-within .lwr { opacity: 1 }
            .{{scope}} .lwb rect[tabindex] { outline: none }
            </style>
            """);

        figure.Append($"""<figure class="{scope}" style="margin:0"><figcaption style="font-size:13px;font-weight:700;color:{theme.TextPrimary};margin-bottom:4px">{Text(chart.Title)}{Unit(chart)}</figcaption>""");
        figure.Append(svg);

        // A legend for two or more, never for one: with a single colour the title has already said
        // what is plotted, and a box with one swatch restates it.
        if (chart.Series.Count > 1) {
            figure.Append($"""<div style="font-size:12px;color:{theme.TextSecondary};margin-top:2px">""");

            for (var s = 0; s < chart.Series.Count; s++) {
                figure.Append($"""
                    <span style="margin-right:12px"><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:{theme.For(s)};margin-right:4px"></span>{Text(chart.Series[s].Name)}</span>
                    """);
            }

            figure.Append("</div>");
        }

        figure.Append(Table(chart, theme, endpoint));
        figure.Append("</figure>");

        return figure.ToString();
    }

    private static string Table(Chart chart, ChartTheme theme, string endpoint) {
        var rows = new StringBuilder();

        if (chart.Form == ChartForm.Columns) {
            foreach (var category in chart.Categories) {
                rows.Append(Row(chart, endpoint, category.Name, category.Name, Comma(category.Value)));
            }
        }
        else {
            foreach (var series in chart.Series) {
                foreach (var reading in series.Readings.OrderBy(one => one.At)) {
                    rows.Append(Row(
                        chart, endpoint, reading.At.ToString("u"),
                        chart.Series.Count > 1 ? $"{series.Name} · {Moment(reading.At)}" : Moment(reading.At),
                        Comma(reading.Value)));
                }
            }
        }

        return $"""
            <details style="margin-top:6px"><summary style="font-size:12px;color:{theme.TextSecondary};cursor:pointer">The numbers</summary>
            <table style="font-size:12px;margin-top:4px"><tr><th>When</th><th>{Text(chart.Unit is {Length: > 0} unit ? unit : "Value")}</th></tr>{rows}</table></details>
            """;
    }

    private static string Row(Chart chart, string endpoint, string value, string label, string shown) =>
        chart.DrillRoute is null
            ? $"<tr><td>{Text(label)}</td><td>{Text(shown)}</td></tr>"
            : $"""<tr><td><a>{Text(label)}</a><cwdb-action action="call" endpoint="{Text(endpoint)}">{Action(chart, value)}</cwdb-action></td><td>{Text(shown)}</td></tr>""";

    /// <remarks>Text wears text tokens. A light categorical hue is illegible as words.</remarks>
    private static string Label(
        double x, double y, string text, string colour, string anchor, int size = 11, bool bold = false) =>
        $"""
        <text x="{N(x)}" y="{N(y)}" font-size="{size}" fill="{colour}" text-anchor="{anchor}"{(bold ? " font-weight=\"700\"" : "")}>{Text(text)}</text>
        """;

    /// <summary>Three ticks at round numbers, which is as many as this height carries.</summary>
    private static IEnumerable<double> Ticks(double top) {
        if (top <= 0) {
            yield return 0;

            yield break;
        }

        var step = Step(top / 3);

        for (var value = 0d; value <= top + step / 2; value += step) {
            yield return value;
        }
    }

    /// <summary>The next 1, 2 or 5 at this magnitude, so a tick is a number a reader recognises.</summary>
    private static double Step(double rough) {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(rough, 1e-9))));
        var normalised = rough / magnitude;

        return (normalised <= 1 ? 1 : normalised <= 2 ? 2 : normalised <= 5 ? 5 : 10) * magnitude;
    }

    /// <summary>The top of the axis, rounded up to a tick so the highest value is inside the plot.</summary>
    private static double Ceiling(double highest) {
        if (highest <= 0) {
            return 0;
        }

        var step = Step(highest / 3);

        return Math.Ceiling(highest / step) * step;
    }

    /// <summary>Axis ticks are comma'd in full; a tick that rounds is a tick that lies.</summary>
    private static string Comma(double value) =>
        value == Math.Floor(value)
            ? ((long)value).ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>A direct label has less room than an axis, so it shortens rather than overflows.</summary>
    private static string Compact(double value) => Math.Abs(value) switch {
        >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => Comma(value)
    };

    private static string Moment(DateTimeOffset at) =>
        at.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// A label that will not fit is shortened here rather than clipped by the mark.
    /// </summary>
    /// <remarks>
    /// Cropping the end of a category name is worse than shortening it, because the reader cannot
    /// tell that anything is missing. An ellipsis says so.
    /// </remarks>
    private static string Clip(string name, double band) {
        var room = Math.Max(3, (int)(band / 6.2));

        return name.Length <= room ? name : name[..Math.Max(1, room - 1)] + "…";
    }

    private static string Unit(Chart chart) =>
        chart.Unit is { Length: > 0 } unit ? $" ({WebUtility.HtmlEncode(unit)})" : "";

    private static string Text(string value) => WebUtility.HtmlEncode(value);

    private static string Quote(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);

    private static string N(double value) =>
        Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
}
