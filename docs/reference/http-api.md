# HTTP API

The harness exposes its interpreter over HTTP, so a click-through test can be written in any
language with nothing but an HTTP client.

```
POST /api/dashboards                                       body: dashboard JSON, as a JSON string
                                                           -> { id }
GET  /api/dashboards/{id}                                  -> dashboard
PUT  /api/dashboards/{id}/state                            body: { name, accountId, theme, relativeMinutes }
                                                           -> dashboard
POST /api/dashboards/{id}/refresh                          re-invokes per updateOn  -> dashboard
GET  /api/dashboards/{id}/widgets/{wid}                    -> widget
GET  /api/dashboards/{id}/widgets/{wid}/actions            -> [ action ]
POST /api/dashboards/{id}/widgets/{wid}/actions/{index}    body: { field: value }  -> widget
POST /api/dashboards/{id}/widgets/{wid}/describe           -> widget
POST /api/lint                                             body: html, as a JSON string -> [ finding ]
```

## The two ids

`{id}` is what `POST /api/dashboards` answered with, or the reserved `default` for the dashboard the
harness loaded from `--dashboard` — the one the page is showing.

`{wid}` is `widget-1`, `widget-2`, `widget-3`, numbered by position among the **custom** widgets in
the body. A dashboard body carries no widget id of its own, so the harness assigns one on parse and
it is stable for as long as the file is. `GET /api/dashboards/{id}` lists them, and asking for one
that is not there names the ones that are:

```json
{ "type": "NotFound", "message": "This dashboard has no widget '0'. It has: widget-1." }
```

## Posting a dashboard

The dashboard goes up as a JSON **string** rather than as an object, so the harness stores exactly
the bytes a `--dashboard` file would have held and parses them the same way:

```
curl -X POST http://localhost:5080/api/dashboards \
     -H 'Content-Type: application/json' \
     -d "$(jq -Rs . < dashboard.json)"
```

A posted dashboard is its own: its own state, its own widgets, its own history. Two tests running at
once do not collide and neither disturbs the page a developer has open.

## A widget

```json
{
  "kind": "Html",
  "html": "<p>Press it.</p><a class=\"btn\">Reset</a><cwdb-action …>",
  "styles": ".lw-widget { font-family: \"Amazon Ember\", … }",
  "event": "{\"echo\":\"…\",\"widgetContext\":{…}}",
  "raw": "\"\\u003Cp\\u003EPress it.\\u003C/p\\u003E…\"",
  "functionError": null,
  "milliseconds": 88.606,
  "forms": {},
  "actions": [ … ],
  "findings": []
}
```

| Field | What it is |
|---|---|
| `kind` | `Html`, `Markdown` or `Json` — how the console would display what came back |
| `html` | What the console renders, after the sanitizer |
| `styles` | The console's default stylesheet for the dashboard's theme, or empty when the widget opted out |
| `event` | The JSON the console would have sent, which is the thing hardest to get right |
| `raw` | The Invoke response as it came back, for a test asserting on the wire |
| `functionError` | The Invoke API's `X-Amz-Function-Error`, or null. The function failing to answer at all |
| `milliseconds` | How long the invoke took |
| `forms` | The widget's named fields and the values the function rendered them with |
| `actions` | Every `cwdb-action`, in document order |
| `findings` | The [linter's](/reference/linter) output |

**`findings` is the assertion to write.** A widget that threw renders an error page, and a function
that answered with an object renders as text: both come back as a body and nothing about the shape
says the invocation failed. Empty findings is what says it did not.

## Clicking

An action is named by its index among the ones the widget rendered:

```json
{
  "index": 0,
  "text": "Reset",
  "kind": "Call",
  "display": "Widget",
  "confirmation": "Discard the edited query?",
  "event": "Click",
  "parameters": { "route": "/", "echo": "<h2>It was reset.</h2>" }
}
```

The click's body is the widget's form fields as they are now, as a flat object of strings:

```
curl -X POST http://localhost:5080/api/dashboards/$id/widgets/widget-1/actions/0 \
     -H 'Content-Type: application/json' \
     -d '{"query":"fields @timestamp | limit 5"}'
```

A field the viewer did not touch keeps what the function rendered, which the interpreter works out —
so a test sends only what it typed. `{}` is a click that changed nothing.

A widget has to be read before it can be clicked, because the actions come from what is on screen:

```json
{ "type": "Conflict", "message": "Widget 'widget-1' has not been rendered, so it has no action to fire. Open it first." }
```

## Linting

The linter on its own, over HTML that has not been anywhere near a function. A widget author in any
language can check what the console would do to their markup without deploying it, running it, or
having an AWS account:

```
curl -X POST http://localhost:5080/api/lint \
     -H 'Content-Type: application/json' \
     -d '"<a onclick=\"go()\">Go</a>"'
```

```json
[ { "rule": "removed-handler",
    "message": "onclick is stripped, so this element does nothing when clicked. …",
    "where": "onclick" } ]
```

## When something is wrong

Every error is the same shape, and the message is the harness's own rather than a generic one:

| Status | When |
|---|---|
| 400 | The body is not the shape the endpoint takes. `type` is `ValidationError` and `errors` names the field |
| 404 | No dashboard with that id, no widget with that id, or no action at that index |
| 409 | The widget has not been rendered yet |

## What this is not

The C# driver in [testing a widget](/guide/testing-a-widget) does not go through this API. It drives
the application's invocation loop in process and uses the same interpreter directly.
