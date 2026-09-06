# cwdb-action

A `<cwdb-action>` element defines a behaviour on the element **immediately before it**. Its content
is either HTML to display or a JSON object of parameters to send.

The element it binds to is positional. There is no `for` attribute and no selector. A `cwdb-action`
with nothing before it does nothing.

## Attributes

| Attribute | Values | Default | Meaning |
|---|---|---|---|
| `action` | `call`, `html` | `html` | `call` invokes a Lambda with the JSON content as parameters. `html` displays the HTML content. |
| `display` | `popup`, `widget` | `widget` | `widget` replaces the widget's content. `popup` shows it in a modal. |
| `endpoint` | Lambda ARN | | Required when `action` is `call`. |
| `confirmation` | text | | A message the user must acknowledge first, with a cancel. |
| `event` | `click`, `dblclick`, `mouseenter` | `click` | `mouseenter` works only with `html`. |

## Examples

These are AWS's, verbatim.

Call a Lambda and show the result in a popup, after a confirmation:

```html
<a class="btn">Reboot Instance</a>
<cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:123456:function:rebootInstance" display="popup">
  { "instanceId": "i-342389adbfef" }
</cwdb-action>
```

Show static HTML in a popup, with no invoke at all:

```html
<a>Click me for more info in popup</a>
<cwdb-action display="popup">
  <h1>Big title</h1> More info about <b>something important</b>.
</cwdb-action>
```

Call a Lambda and replace the widget:

```html
<a class="btn btn-primary">Next</a>
<cwdb-action action="call" endpoint="arn:aws:lambda:us-east-1:123456:function:nextPage">
  { "pageNum": 2 }
</cwdb-action>
```

## The endpoint

`endpoint` is a Lambda ARN, and it is usually the function's own. AWS's Logs Insights widget fills it
from the Lambda context's `invokedFunctionArn`, which includes any alias qualifier. Do the same
rather than writing the ARN into a template, so a widget deployed to a second account or behind an
alias keeps working.

## Forms travel with the call

A `call` sends its JSON content as top-level event fields, and the console adds the widget's form
values under `widgetContext.forms.all`. You do not list the fields in the action; they come along.

## Four ways to get it wrong

The console fails these silently. `LambdaWidgets`' [linter](/reference/linter) reports them.

- A `cwdb-action` with no element before it.
- `action="call"` with no `endpoint`.
- `event="mouseenter"` on a `call`.
- Content that is not valid JSON on a `call`.
