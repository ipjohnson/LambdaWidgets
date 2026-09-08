# WidgetName

A CloudWatch custom widget that charts metrics as inline SVG, with a hover readout and a drill-down.

## Run it

Two processes. The widget, under the AWS Lambda Test Tool:

```
dotnet run --project WidgetName
```

The harness, which is the console's side of the contract on your machine:

```
lambda-widgets --dashboard dashboard.json
```

Then open http://localhost:5080. Click things; the widget re-invokes and the page re-renders, the
way a dashboard does.

Set `namespace` in the custom widget's parameters to the metric namespace you want; it defaults
to `AWS/Lambda`. Which metrics are charted is in `WidgetNameApp.cs`.

Charts are SVG rendered on the server, because the console strips JavaScript. Hovering a moment
draws a hairline and writes every series' value under the axis - CSS, no script. `MetricSource.cs`
is the seam: answer `Series` from anywhere and nothing else changes.

## Test it

```
dotnet test
```

The tests drive the widget the way a viewer does - open it, fill a field, click by the text on
screen - and assert on what came back. They never name a route or a payload field, because a viewer
cannot. No AWS account is needed: the data source is substituted at its own interface, so the real
handlers, the real binding and the real views all run.

## Deploy it

Publish ahead-of-time for `provided.al2023`:

```
dotnet publish WidgetName -c Release -r linux-arm64 -p:PublishAot=true
```

The function handler is the assembly name, `customWidgetWidgetName`. Add a custom widget to a
dashboard with that function's ARN as its endpoint.

Its execution role needs `cloudwatch:GetMetricData`.

## Routes come from the generated links, never from a literal

```razor
@Widget.Button("Run", Links.Pages.Search(), primary: true)
```

`Links` is generated from the `[Get]` handlers in `WidgetNameApp.cs`. Rename a handler and this line
fails to compile, which is the point: with a literal the widget would still render and the button
would silently do nothing. In a handler, where there is no `Links`, use
`WidgetNameApp.Routes.Pages.Search()` for the same paths as plain strings.
