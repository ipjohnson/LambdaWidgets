# Writing a widget in C#

A widget application is a [Hardened](https://github.com/ipjohnson/Hardened.Framework) application on
a widget host. It is web-shaped rather than function-shaped, which is the whole point: pages are
ordinary routes, so the generated `Routes` and `Links` types, the template bases, validation and the
route table all work unchanged.

```csharp
[HardenedModule]
[LambdaWidgetModule]
public partial class LogsSearch { }

public class Pages {
    [Get("/")]
    public LandingPage Index(IWidgetContext context) => new(context);

    [Get("/search")]
    public async Task<ResultsPage> Search(SearchRequest request, IWidgetContext context) { ... }
}

public class SearchRequest {
    public string LogGroups { get; set; } = "";
    public string Query { get; set; } = "";
    public string? Cursor { get; set; }
}
```

Every widget route is `[Get]`. The console sends no method, the host looks a route up as GET, and a
`[Post]` handler would be unreachable.

## How an event becomes a request

A reserved top-level field `route` holds the path of the handler to run, which is the string a
generated link produced. Absent means `/`. It becomes the request path, and the route table matches
it with path parameters and constraints as usual.

The query string is built by merging three sources, later winning: `widgetContext.params`, then the
top-level event fields, then `widgetContext.forms.all`. A handler binds one request object out of
that with the ordinary binding and never sees where a value came from.

A dashboard author who wants a widget to open on a specific tool sets `route` in the widget's
parameters.

## The helpers

```razor
@inherits LambdaWidgets.Runtime.WidgetTemplate<ResultsPage>
<form>
  <input name="logGroups" value="@Model.LogGroups" size="60">
  <textarea name="query" rows="3">@Model.Query</textarea>
</form>
@Widget.Button("Run query", Links.Pages.Search(), primary: true)

<table>
  @foreach (var row in Model.Rows) {
    <tr><td>@Widget.Link(row.Id, Links.Rows.Row(row.Id), display: Display.Popup)</td></tr>
  }
</table>
@Widget.Link("Next", Links.Pages.Search(), ("cursor", Model.NextCursor))
```

`Widget.Link` writes the anchor and, immediately after it, the `cwdb-action` with `action="call"`,
the `endpoint` taken from `ILambdaContext.InvokedFunctionArn`, and a JSON object of `route` plus the
fields you gave it. `Widget.Button` is the same with the `btn` classes.

The route argument is the string from the generated `Links` or `Routes` type, never a literal. Rename
a handler and the template breaks at build time, on its own line.

Fields are name and value pairs written by hand, so no serializer runs and the AOT analyzer has
nothing to complain about. Numbers and booleans travel as strings and are parsed by the binding on
the way back.

## State

State goes in the call's fields. A paging cursor is `("cursor", Model.NextCursor)`, not a hidden
input. There are no cookies and no session, and anything that redraws the widget sends the
configured parameters again.

## Describe

When the event carries `describe`, the host returns the application's documentation and calls no
handler. Write it as markdown with a fenced `yaml` block of example parameters, and the console's
*Get documentation* button will lift that block into the widget's parameters editor.

AWS recommends supporting `describe` even if the answer is an empty string.
