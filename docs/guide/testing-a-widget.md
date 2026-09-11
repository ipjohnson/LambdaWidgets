# Testing a widget

A widget's tests are click-throughs. `LambdaWidgets.Testing` drives the application's generated
`Invoke` in process, so a test needs neither the test tool nor the harness running.

```csharp
// Bootstrap.cs
[assembly: WidgetTesting]
[assembly: HardenedTestEntryPoint(typeof(LogsSearchApp))]
```

```csharp
public class LogsSearchTests {
    [HardenedTest]
    public async Task ASearchRunsOverTheDashboardsRange(IWidgetDriver widget) {
        widget.State = widget.State with {
            TimeRange = WidgetTimeRange.Relative(TimeSpan.FromHours(1))
        };

        await widget.Open();                            // route "/" with the configured params
        widget.Fill("query", "fields @timestamp | limit 5");

        var page = await widget.Click("Run query");     // by bound element text, or by index

        Assert.Contains("@timestamp", page.Html);
        Assert.Empty(page.Findings);

        await widget.Refresh();                         // configured params again, per updateOn
        Assert.Contains("Log groups", widget.Html);
    }
}
```

`Open`, `Click`, `Fill`, `Refresh`, `Decline` and `Describe` cover the interactions, `Params` is the
widget's configured parameters, and `State` covers everything else in `widgetContext`. `Click`
accepts a `confirmation`; `Decline` is the other answer, and asserting that declining really does
nothing is the property a confirmation exists for.

The driver interprets responses through `LambdaWidgets.Dashboard`, the same library the harness
uses. A click that works in a test works in the harness, and both are wrong in the same way if the
interpreter is wrong.

## What says the widget worked

`page.Findings` carries the [linter's](/reference/linter) output. Asserting it is empty is how a test
catches HTML the console would silently strip — and how it catches a widget that did not render at
all.

**A failed invocation reaches the console as a body, the same as a successful one.** A handler that
threw answers an error page, and a function that returned an object is displayed as JSON. Both have
non-empty `Html`, so a test asserting only on the HTML passes on a widget that showed a viewer
nothing. `page.Failed` says so directly, and each raises a finding:

```csharp
var page = await widget.Open();

Assert.False(page.Failed);
Assert.Empty(page.Findings);
```

Set `WIDGET_ERROR_DETAIL` in the function's environment to put the exception's own message on the
error page while developing. It is off by default, because that page reaches a real dashboard.

## In another language

The harness exposes an [HTTP API](/reference/http-api) that does the same thing over HTTP. A Python
or Node author drives a click-through against it with an HTTP client and nothing else.
