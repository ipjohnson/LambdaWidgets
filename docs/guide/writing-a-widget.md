# Writing a widget in C#

A widget application is a [Hardened](https://github.com/ipjohnson/Hardened.Framework) application on
a widget host. It is web-shaped rather than function-shaped, which is the whole point: pages are
ordinary routes, so the generated `Routes` and `Links` types, the template bases, validation and the
route table all work unchanged.

The fastest way in is a [template](/guide/templates). What follows is what one produces.

## The application

One file, and it does one thing: compose the modules and register services.

```csharp
// OrdersSearchApp.cs
using Amazon.CloudWatchLogs;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Shared.Runtime.Attributes;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace OrdersSearch;

[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class OrdersSearchApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAmazonCloudWatchLogs>(_ => new AmazonCloudWatchLogsClient());
}
```

`[LambdaWidgetModule]` composes `[HardenedWebModule]` and `[LambdaRuntimeModule]`, so the widget
gets the web request pipeline on the Lambda host. `[Enable<WidgetTemplates>]` generates
`OrdersSearchAppWidgetTemplates<TModel>`, the base your Razor views inherit.

`IServiceCollectionConfiguration` is only needed when there is something to register. A widget with
no AWS client is `public partial class OrdersSearchApp { }`.

## The pages

A separate file, because handlers are ordinary classes and a widget usually has more than one.

```csharp
// Pages.cs
using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace OrdersSearch;

public class Pages(ILogQueries queries) {

    [Get("/")]
    [Output<Views.LandingPage>]
    public SearchPage Index([FromWidget] SearchRequest request, IWidgetContext context) =>
        new(request.Query, request.LogGroups, context, Results: null);

    [Get("/search")]
    [Output<Views.ResultsPage>]
    public async Task<SearchPage> Search(
        [FromWidget] SearchRequest request,
        IWidgetContext context,
        CancellationToken cancellationToken) {
        var (start, end) = context.TimeRange.Effective;

        var results = await queries.Run(
            request.LogGroups, request.Query, start, end, request.Limit, cancellationToken);

        return new SearchPage(request.Query, request.LogGroups, context, results);
    }
}

public class SearchRequest {
    public string LogGroups { get; set; } = "";
    public string Query { get; set; } = "";
    public int Limit { get; set; } = 20;
}

public record SearchPage(string Query, string LogGroups, IWidgetContext Context, LogResults? Results);
```

Three things there are not optional.

**Every route is `[Get]`.** The console sends no method, the host looks a route up as GET, and a
`[Post]` handler would be unreachable.

**`[Output<TView>]` names the view.** Without it the handler answers its model as JSON and the
console renders the raw text.

**`[FromWidget]` on the request object.** Hardened binds a complex parameter from the body and
refuses one on a `[Get]` with `HRDR010`. `[FromWidget]` is the custom binding that reads it out of
the merged widget event instead.

`IWidgetContext` is the dashboard: its time range, theme, period, account, and the widget's
configured parameters. Read `TimeRange.Effective` rather than `TimeRange.Start`, because the console
sends a `zoom` when the viewer has zoomed a chart and a widget that ignores it queries the wrong
window.

## How an event becomes a request

A reserved top-level field `route` holds the path of the handler to run, which is the string a
generated link produced. Absent means `/`. It becomes the request path, and the route table matches
it with path parameters and constraints as usual.

The query string is built by merging three sources, later winning: `widgetContext.params`, then the
top-level event fields, then `widgetContext.forms.all`. A handler binds one request object out of
that and never sees where a value came from.

A dashboard author who wants a widget to open on a specific tool sets `route` in the widget's
parameters.

## The views

```razor
@* Views/LandingPage.cshtml *@
@inherits OrdersSearch.OrdersSearchAppWidgetTemplates<OrdersSearch.SearchPage>
@Widget.Root()
<form>
  <label>Log groups</label>
  <input name="logGroups" value="@Model.LogGroups" size="60">
  <label>Query</label>
  <textarea name="query" rows="4">@Model.Query</textarea>
</form>
@Widget.Button("Run query", Links.Pages.Search(), primary: true)
@Widget.Confirm("Reset", "Discard the edited query?", Links.Pages.Reset())
@Widget.EndRoot()
```

**Inherit the generated base, not `WidgetTemplate<TModel>`.** The generated one is what carries
`Links`; inheriting the framework type directly gives you the helpers with no generated links, and
every route in the file becomes a literal.

`@Widget.Button` writes the anchor and, immediately after it, the `cwdb-action` with
`action="call"`, the `endpoint` taken from `ILambdaContext.InvokedFunctionArn`, and a JSON object of
`route` plus any fields. `@Widget.Link` is the same without the button classes, `@Widget.Confirm`
adds a confirmation the console enforces, and `@Widget.Detail` opens the result in a popup.

`@Widget.Root()` opens a `div` carrying the dashboard's theme as a class, so the widget's own CSS
can style light and dark without asking.

### The route argument is never a literal

```razor
@Widget.Button("Run query", Links.Pages.Search(), primary: true)
```

Rename `Search` and this line fails during the build:

```
Views/LandingPage.cshtml(9,41): error CS1061: 'OrdersSearchApp.Links.PagesLinks' does not
contain a definition for 'Search'
```

With `"/search"` written out, the widget renders perfectly and the button does nothing at all. That
is the failure custom widgets are worst at surfacing, and the generated `Links` is the only thing
that catches it. In a handler, where there is no `Links` property, `OrdersSearchApp.Routes.Pages.Search()`
is the same paths as plain strings.

Fields are name and value pairs written by hand, so no serializer runs and the AOT analyzer has
nothing to complain about. Numbers and booleans travel as strings and the binding parses them back.

## State

State goes in the call's fields. A paging cursor is a field on the link the viewer is about to
click:

```razor
@Widget.Link("Next", Links.Pages.LookUp(), ("cursor", Model.Page.Cursor))
```

Not a hidden input, and not anything on the server. There are no cookies and no session, and
anything that redraws the widget sends the configured parameters again. A refresh puts the widget
back on its landing page, which is worth a test.

## Describe

When the event carries `describe`, the host returns the application's documentation and calls no
handler:

```csharp
[SingletonService(As = typeof(IWidgetDocs))]
public class OrdersSearchDocs : IWidgetDocs {
    public string Markdown => """
        Search a log group over the dashboard's time range.

        ```yaml
        logGroups: /aws/lambda/orders
        query: fields @timestamp, @message | limit 20
        ```
        """;
}
```

The console's *Get documentation* button lifts that fenced `yaml` block straight into the widget's
parameters editor. AWS recommends supporting `describe` even if the answer is an empty string.

## Next

[Charting data](/guide/charting-data) · [Testing a widget](/guide/testing-a-widget) ·
[Running the harness](/guide/running-the-harness) · [Deploying](/guide/deploying)
