# Charting data

`LambdaWidgets.Charts` draws a query's answer as SVG, inside the widget, with no JavaScript and no
chart library in the browser. The console strips `<script>`, so a chart has to arrive already drawn.

```csharp
[HardenedModule]
[LambdaWidgetModule]
[ChartsModule]
[Enable<ChartTemplates>]
public partial class GraphApp { }
```

A view then has `@Draw` beside `@Widget` and `@Links`:

```razor
@inherits Graph.GraphAppChartTemplates<Graph.GraphPage>
@Widget.Root()
@Draw(Model.Requests)
@Widget.EndRoot()
```

## Describe the chart, do not draw it

A handler builds a `Chart` and hands it over. Nothing in a widget writes SVG.

```csharp
Chart.Over("Errors by kind",
        Series("timeout", rows),
        Series("throttled", rows),
        Series("5xx", rows))
    .In("per 5 min")
```

Three shapes, and the data picks between them:

| Call | What it is for | What it draws |
|---|---|---|
| `Chart.Over(title, series…)` | change over time | a line per series |
| `Chart.Across(title, categories)` | magnitude across named things | columns from one baseline |
| `Chart.Number(title, value)` | a headline | one large number |

`Chart.Across` with a single category returns a `Chart.Number`. A one-bar bar chart carries no
comparison, so the number is the clearer answer, and the library makes that call rather than leaving
it to whoever writes the query.

`Chart.Over` past three series folds the tail into one summed series called `Other`, ordered by
total so the three that keep their identity are the three worth looking at. Summed rather than
dropped: a chart that silently omits data is worse than one that groups it.

## The colours are validated, not chosen

Four categorical slots per theme, checked against the console's own surfaces for the lightness band,
the chroma floor, colour-vision separation and the normal-vision floor. The dashboard's theme
arrives in `widgetContext`, so the right set is written straight into the SVG — no media query, and
no second definition to drift.

**Colour goes by position, never by rank.** A series keeps its colour when the one above it
disappears, so a reader who learned that timeouts are blue keeps that. This makes the argument order
a contract: pass your series in a fixed order, and pass one with no readings in this window rather
than leaving it out.

Drawing more series than the palette distinguishes throws. Cycling would hand two series the same
hue, and a repeated colour looks like data.

## Pointing at the chart

Hover or focus a moment on a line chart and a hairline marks it, a ringed dot appears on every
series, and the values are written under the axis:

```
14:00   — 148 timeout   — 60 throttled   — 26 5xx
```

Every series at once, so the pointer never has to land on a two-pixel line. Under the plot rather
than floating over it, because a card that follows the pointer covers the lines on either side of
the moment it describes — which is the comparison the reader came for.

On a column chart the whole band is the hit target, the hovered column is outlined, and the readout
spells out a name the axis had to truncate.

**This is CSS, not script.** `:hover` and `:focus-within` over one transparent band per data
position. Each readout carries `opacity="0"` and the stylesheet only turns it on, so a console that
dropped the `<style>` block would show a plain chart rather than every readout at once.

::: warning Unverified
Whether the console keeps a `<style>` block, and whether it binds a `cwdb-action` inside an `<svg>`,
are both on the probe's list. Neither is load-bearing: without the stylesheet the chart still draws,
and the table under it carries the same links as the marks.
:::

Nothing is hover-only. Every chart ships a `<details>` table with every value in it, so the numbers
are reachable without a pointer at all.

## Drilling in

```csharp
Chart.Across("Errors by function", rows).DrillingTo(Links.Pages.Function(), "function")
```

Each mark and each table row becomes a `cwdb-action` that re-invokes the widget at that route with
the clicked name. As everywhere else in this framework, the route comes from the generated `Links`
rather than a literal, so renaming the handler breaks the build instead of the button.

## Where the data comes from

`Series` and `Category` are the only shapes. Shape a Logs Insights `stats count(*) by bin(5m)`, a
DynamoDB query or a CloudWatch `GetMetricData` into them and nothing else in the widget changes.
`samples/Graph` makes its numbers up so it opens with no AWS account; `Metrics.cs` is the one file
a real widget replaces.

## What it will not do

**No dual axis.** Two measures of different scale get two charts. A second y-axis invents a
correlation that is not in the data.

**No number on every point.** A line chart labels its endpoint and nothing else; the axis, the
readout and the table carry the rest.

**No area wash under several lines.** Overlapping fills muddy every hue they cross. One series gets
a wash, several do not.
