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
