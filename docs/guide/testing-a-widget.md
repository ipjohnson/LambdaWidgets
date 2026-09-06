# Testing a widget

::: warning Not shipped yet
Item 4 builds `LambdaWidgets.Testing`. See [status](/status).
:::

A widget's tests are click-throughs. `LambdaWidgets.Testing` drives the application's generated
`Invoke` in process, so a test needs neither the test tool nor the harness running.

```csharp
[HardenedTest]
[WidgetTesting]
public class LogsSearchTests {
    [Fact]
    public async Task Search_runs_with_the_dashboard_range(IWidgetDriver widget) {
        widget.State.TimeRange = TimeRange.Relative(TimeSpan.FromHours(1));
        await widget.Open();                            // route "/" with the configured params
        widget.Fill("query", "fields @timestamp | limit 5");

        var page = await widget.Click("Run query");     // by bound element text, or by index

        Assert.Contains("@timestamp", page.Html);
        Assert.Equal(Display.Widget, page.Display);
        Assert.Empty(page.Findings);

        await widget.Refresh();                         // configured params again, per updateOn
        Assert.Contains("Log Groups", widget.Html);
    }
}
```

`Open`, `Click`, `Fill`, `Refresh`, `Resize` and `Describe` cover the interactions, and `State`
covers everything in `widgetContext`. `Click` honours a `confirmation` by default and exposes
`Cancel` for the other branch.

The driver interprets responses through `LambdaWidgets.Dashboard`, the same library the harness
uses. A click that works in a test works in the harness, and both are wrong in the same way if the
interpreter is wrong.

`page.Findings` carries the [linter's](/reference/linter) output. Asserting it is empty is how a test
catches HTML the console would silently strip.

## In another language

The harness exposes an [HTTP API](/reference/http-api) that does the same thing over HTTP. A Python
or Node author drives a click-through against it with an HTTP client and nothing else.
