# The console contract

Everything in this section describes CloudWatch as it behaves today, not anything this repository
ships. It was read from AWS's four custom widget pages and the Logs Insights widget in
`aws-samples/cloudwatch-custom-widgets-samples`. It is useful whether or not you use LambdaWidgets.

A handful of details are marked **unverified**. Those are the ones the documentation does not state
and the samples do not settle. They are being checked against a real dashboard, and this page will
say what was observed.

## The four parts

**[The event](/reference/event)** is what the console sends. Configured parameters at the top level,
a `describe` flag when the console wants documentation, and a `widgetContext` object carrying the
dashboard's state.

**[cwdb-action](/reference/cwdb-action)** is what your HTML sends back. It binds a behaviour to the
element before it: call a Lambda, or show some HTML, in the widget or in a popup.

**[Rendering rules](/reference/rendering-rules)** are what survives. JavaScript is stripped. Most
HTML, `style` blocks and inline SVG are kept. The dashboard's own styles apply unless you turn them
off.

**Responses** are three shapes. An HTML string is rendered. `{"markdown": "..."}` is rendered as
markdown. Anything else is displayed as JSON.

## Describe

When the event carries a `describe` flag, the function returns markdown documentation and does
nothing else. The console's *Get documentation* button sends it, and lifts the first fenced `yaml`
block out of the markdown into the widget's parameters editor.

AWS strongly recommends supporting it, even if the answer is an empty string.

## Dashboard behaviour

Custom widgets are refreshed, auto-refreshed, resized, moved, and react to the dashboard's time
range like any other widget. Each of those re-invokes the function with the widget's *configured*
parameters, so a widget that has navigated away from its landing page returns to it.

The widget definition carries `updateOn` flags for `refresh`, `resize` and `timeRange`. Whether each
one switches its trigger off, and the exact JSON, is **unverified**.

A custom widget does not run until the viewer allows it, once or always, per widget or per
dashboard. The viewer can also deny it.

A widget from one account can sit on dashboards in other accounts through console cross-account, and
widgets work on your own website through dashboard sharing.

AWS recommends the prefix `customWidget` on function names, so a dashboard author can tell which
functions are safe to add.

## What the contract does not carry

**No JavaScript**, so nothing happens on the client. Every interaction is a round trip.

**No state between invocations.** Whatever the next page needs rides in the action's JSON.

**No viewer identity**, as far as the documentation says. This is **unverified**; it is being checked
by logging a full event. Until shown otherwise, the audit trail of an action is the CloudTrail record
of the invoke joined to the function's log by request id.
