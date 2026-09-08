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
