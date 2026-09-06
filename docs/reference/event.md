# The event

Every invocation carries the widget's configured parameters as top-level fields, a `describe` flag
when the console asks for documentation, and a `widgetContext` object.

This is AWS's example, verbatim.

```json
{
  "widgetContext": {
    "dashboardName": "Name-of-current-dashboard",
    "widgetId": "widget-16",
    "accountId": "012345678901",
    "locale": "en",
    "timezone": { "label": "UTC", "offsetISO": "+00:00", "offsetInMinutes": 0 },
    "period": 300,
    "isAutoPeriod": true,
    "timeRange": {
      "mode": "relative",
      "start": 1627236199729,
      "end": 1627322599729,
      "relativeStart": 86400012,
      "zoom": { "start": 1627276030434, "end": 1627282956521 }
    },
    "theme": "light",
    "linkCharts": true,
    "title": "Tweets for Amazon website problem",
    "forms": { "all": {} },
    "params": { "original": "param-to-widget" },
    "width": 588,
    "height": 369
  }
}
```

## Times

Every time is epoch milliseconds.

`zoom` is present only when the user has zoomed a chart. AWS's own sample takes the effective range
as `timeRange.zoom || timeRange`, and so should you. A tool that queries for the dashboard's range
should read it from here rather than offering a date picker of its own.

## Where a value arrives

Three places, and they are not interchangeable.

| Source | Where it lands |
|---|---|
| The widget's configured parameters | Top level, and repeated under `widgetContext.params` |
| A parameter sent by a `cwdb-action` | Top level. **Not** inside `params` |
| A form field in the widget | `widgetContext.forms.all`, keyed by the field's `name` |

That is why AWS's Logs Insights sample reads `form.query || event.query`: the form value if the user
typed one, otherwise the configured parameter.

## Forms

Form fields travel in `widgetContext.forms.all`, keyed by `name`. A field without a `name` never
arrives.

The AWS Logs Insights widget wraps an `input` named `logGroups` and a `textarea` named `query` in a
`form`, binds a button to a `call` action, and reads `widgetContext.forms.all.logGroups` with the
top-level field as its fallback.

Whether a `form` wrapper is required, or every named field in the widget is collected, is
**unverified**.
