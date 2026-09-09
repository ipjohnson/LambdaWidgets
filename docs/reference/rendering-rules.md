# Rendering rules

The console does not render what your function returns. It renders what survives its sanitizer, with
the dashboard's own styles applied on top.

## What is removed

JavaScript is stripped before rendering. `iframe` and `use` elements are removed.

The stated reason is privilege. Your function's HTML runs inside the console with the viewer's
session, so code in it would run as them. This is not configurable, and it is the constraint every
other part of the design follows from.

## What is kept

Most HTML. CSS through `style` attributes and `<style>` blocks. Inline SVG.

Which further tags survive, whether `img` with `data:` and `https:` sources renders, and how
`details`, `button` and the various `input` types behave, is **unverified**. It is being checked by
feeding HTML through AWS's own Echo widget on a real dashboard.

## Default styles

The console styles your content to match the dashboard. `table`, `select`, `h1` to `h3`, `pre`,
`input` and `textarea` all get default styling.

Two classes turn an anchor into a button:

```html
<a class="btn">Secondary</a>
<a class="btn btn-primary">Primary</a>
```

A single element with class `cwdb-no-default-styles` turns the defaults off.

## Themes

`widgetContext.theme` is `light` or `dark`. There is no media query that will tell you, because the
widget is not in its own document with its own colour scheme. Read the field and style both.

## Writing HTML that survives

The failure to expect is silent: your markup works in a browser, and part of it is simply absent in
the console. Three habits avoid most of it.

Return HTML, not a page. No `<html>`, no `<head>`, no `<script>` you expect to run.

Put behaviour in `cwdb-action`, never in an event-handler attribute. `onclick` is removed.

Check what was stripped rather than assuming. The harness's inspector lists every removal, and the
[linter](/reference/linter) reports the ones that are always mistakes.

## Stylesheets

A `<style>` block is allowed anywhere in the returned HTML, and there is no separate stylesheet to
link — a widget serves no static files. Every response therefore carries the CSS it needs, and a
re-invocation replaces the previous response wholesale.

The default styling applies to `table`, `select`, `h1` to `h3`, `pre`, `input` and `textarea`, plus
`btn` and `btn btn-primary` on an anchor. A single element anywhere in the returned HTML carrying
`cwdb-no-default-styles` turns all of it off for that widget. AWS's docs put that class on a
`<span>`; their own samples put it on the `<table>` being overridden. Either works — it is a
property of the document, not of the element.

`:hover` is documented as supported: *"HTML can include CSS selectors such as `:hover` which can
trigger animations or different CSS effects."*

### The container is the console's, not the widget's

The console wraps the returned HTML in a container it owns and puts its theme class on it. The one
confirmed name is **`cwdb-theme-dark`**, which AWS's `costExplorerReport` sample selects on as an
ancestor:

```css
.cwdb-theme-dark td, .cwdb-theme-dark th { color: white; background-color: #2A2E33; }
```

A widget writes no wrapper of its own; one would be inert in production. The harness reproduces this
by putting `cwdb-theme-dark` on the element it renders a widget into.

### Whether one widget's CSS reaches another is unverified

Every AWS sample writes bare, unscoped selectors — `td { white-space: nowrap }`, and
`cloudWatchMetricDataTable` goes as far as `td,th{font-family:Arial;font-size:12px;text-align:center}`.
Two of those on one dashboard would visibly wreck each other unless the console scopes each widget's
stylesheet to that widget. Shadow DOM per widget would explain it, and would also explain why
`cwdb-no-default-styles` acts per widget. Nothing AWS publishes says so.

The harness assumes the pessimistic case and does not isolate, because an author who ships a widget
that breaks a colleague's on a shared dashboard finds out from the colleague. See
[status](/status) for the probe that settles it.
