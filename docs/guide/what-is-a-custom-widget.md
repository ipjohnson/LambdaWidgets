# What is a custom widget

A CloudWatch custom widget is a dashboard widget whose content comes from a Lambda function you own.
The dashboard invokes the function, the function returns HTML, and the console renders it in the
widget's rectangle.

That much is a report. The part that makes it a tool is `cwdb-action`: an element in the returned
HTML that binds a behaviour to the element before it. Clicking re-invokes the same function with
parameters you chose and the widget's form fields, and the response replaces the widget or opens a
popup.

So one widget is a whole server-rendered application inside the console. It has a landing page,
links to tools, forms, and results. The function's execution role decides what the tool may do. IAM
on the dashboard and on the function decides who may use it.

## The round trip

```
Browser  ──click, form values──>  CloudWatch console  ──Invoke: event──>  Your Lambda
                                        (viewer's credentials)          (execution role)
Browser  <──rendered widget────   CloudWatch console  <──HTML──────────  Your Lambda
```

Everything travels through the function. There is no JavaScript on the client, because the console
strips it before rendering. That is deliberate: the widget's author must not be able to run code
with the viewer's permissions.

## What that costs you

Three constraints follow from the design, and they shape every widget.

**Every interaction is a round trip.** Nothing happens in the browser. A checkbox that filters a
table filters it by invoking the function again.

**There is no state between invocations.** Whatever the next page needs has to ride in the action's
JSON. A paging cursor is a field on the call, not a hidden input and not a session.

**Anything that redraws the widget resets it.** Refresh, auto-refresh, resize, and a change to the
dashboard's time range all re-invoke the function with the widget's *configured* parameters. A
widget three clicks deep into a tool returns to its landing page.

## Who uses this

Teams inside AWS build operational tools this way. Outside AWS the feature is close to unused. The
official samples are Python and JavaScript scripts, there is no framework for it in any language,
and there is no local development loop.

That gap is what this repository is aimed at. Read [why a harness](/guide/why-a-harness) next, or go
straight to [the console contract](/reference/console-contract) if you want the mechanics.
