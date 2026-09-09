# Linter findings

Every finding here is something the console does silently. Your widget renders, part of it does not
work, and nothing anywhere says why. The linter reports them in the harness's inspector, on every
[HTTP API](/reference/http-api) response, and to the test driver, where an assertion on an empty
finding list turns each one into a failing test.

## Stripped by the sanitizer

- A `script`, `iframe` or `use` element.
- An event-handler attribute such as `onclick`.

These are removed before rendering. See [rendering rules](/reference/rendering-rules).

## Broken cwdb-action

- A `cwdb-action` with no element before it. It binds to nothing.
- `action="call"` with no `endpoint`. There is nothing to invoke.
- `event="mouseenter"` on a `call`. `mouseenter` works only with `html`.
- Content that is not valid JSON on a `call`.

See [cwdb-action](/reference/cwdb-action).

## Fields that never arrive

- A form field without a `name`. It is never collected into `forms.all`, so the handler reading it
  gets nothing and cannot tell that from an empty value.

## Endpoints that go nowhere

- An `endpoint` whose function name has no configured target. In the console this is a failed
  invoke; in the harness it is a finding before you click.

## CSS that reaches other widgets

- A stylesheet rule whose selector does not lead with a class or an id of your own — `td { }`,
  `svg { }`, `table.rows td { }`. Every widget on a dashboard shares one page, so these can match
  whatever the widgets beside yours rendered.

This is the one advisory rule here. Everything above is a defect; this one depends on a question
nobody has answered. AWS's own samples write bare `td { }` freely, which only works if the console
scopes each widget's stylesheet to that widget, and nothing AWS publishes says it does.

The rule assumes it does not, because the two ways of being wrong are not symmetric. If the console
does isolate and we warn anyway, you ignore a finding. If it does not and we stay quiet, you ship a
widget that restyles a colleague's on a shared dashboard and hear about it from the colleague.

`.rows td { }` is correct either way, so the fix costs nothing. See [status](/status) for the probe
that settles it.
