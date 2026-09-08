# HTTP API

The harness exposes its interpreter over HTTP, so a click-through test can be written in any
language with nothing but an HTTP client.

```
POST /api/dashboards                                       body: dashboard JSON, as a JSON string
                                                           -> { id }
PUT  /api/dashboards/{id}/state                            body: time range, theme, period, ...
GET  /api/dashboards/{id}/widgets/{wid}                    -> { html, display, event, raw, findings }
GET  /api/dashboards/{id}/widgets/{wid}/actions            -> [ { index, text, action, display, confirmation, event } ]
POST /api/dashboards/{id}/widgets/{wid}/actions/{index}    body: { forms: { name: value } }
                                                           -> { html, display, event, raw, findings }
POST /api/dashboards/{id}/refresh                          re-invokes per updateOn
POST /api/lint                                             body: html               -> findings
```

The dashboard goes up as a JSON **string** rather than as an object, so the harness stores exactly
the bytes a `--dashboard` file would have held and parses them the same way:

```
curl -X POST http://localhost:5080/api/dashboards \
     -H 'Content-Type: application/json' \
     -d "$(jq -Rs . < dashboard.json)"
```

A test loads a dashboard, sets the state it wants, reads the widget, finds the action it means to
click by its bound element's text, posts to it with the form values, and asserts on the returned
HTML.

`findings` on every response is the [linter's](/reference/linter) output. A test that asserts it is
empty catches HTML the console would strip, which is the failure that is otherwise invisible.

The C# driver in [testing a widget](/guide/testing-a-widget) does not go through this API. It drives
the application's generated `Invoke` in process and uses the same interpreter directly.
