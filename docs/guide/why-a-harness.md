# Why a harness

Developing a custom widget without one means this loop: edit the function, deploy it, open the
dashboard in a browser, click through to the state you were testing, read a CloudWatch log to find
out what went wrong, and start again. Every iteration costs a deploy and a manual click-through, and
the only view of the failure is a log line.

`lambda-widgets` replaces the console in that loop.

```
Browser  ──click, forms──>  lambda-widgets  ──Invoke API──>  test tool, RIE,   ──event──>  Your
                            page + interpreter               SAM local, or AWS            Lambda
Browser  <──render────────  lambda-widgets  <──result──────                    <──HTML───
```

The widget sees the same event and returns the same HTML in both arrangements. Only the middle
changes.

## What it has to get right

A harness that renders your HTML in a browser tab would not be worth much. The console does four
things to that HTML, and a local loop is only useful if it does all four the same way.

**It builds the event.** The configured parameters at the top level and again under
`widgetContext.params`, the dashboard's time range and period, the theme, the widget's size, and the
form values from the last interaction. See [the event](/reference/event).

**It interprets `cwdb-action`.** Which element is bound, whether a click calls a Lambda or shows
HTML, whether the result replaces the widget or opens a popup, and whether a confirmation is
required first. See [cwdb-action](/reference/cwdb-action).

**It sanitizes.** JavaScript is stripped, `iframe` and `use` are removed. HTML that works in your
browser and disappears in the console is the most common way a widget fails. See
[rendering rules](/reference/rendering-rules).

**It re-invokes on the dashboard's own events.** Refresh, resize and a time range change each send
the configured parameters again, which is what makes a widget forget where the user was. A harness
that keeps the current page hides the single most surprising behaviour of the real thing.

All four live in `LambdaWidgets.Dashboard`, a library with no host, no HTTP and no UI. The harness
and the C# test driver both use it, so they cannot drift apart.

## It does one thing the console does not

Each widget gets an inspector: the last event sent, the raw response, what the sanitizer removed,
how long the invoke took, and the linter's findings. The console shows you the rendered result and
nothing else.

## Any language

The harness invokes through the Lambda Invoke API. It does not care what your function is written
in, and it is not a .NET tool that happens to have a web page. A Python or Node author uses the same
binary, and can drive click-through tests against its [HTTP API](/reference/http-api) with nothing
but an HTTP client.
