# Starting from a template

Three `dotnet new` templates, one per shape a custom widget usually takes.

```
dotnet new install LambdaWidgets.Templates

dotnet new lw-logs  -n OrdersSearch     # a Logs Insights query
dotnet new lw-ddb   -n OrderLookup      # a DynamoDB query, paged
dotnet new lw-graph -n OrderMetrics      # a chart over CloudWatch metrics
```

::: warning Prerelease
Everything is on the `0.1.0-rc1000` line, so `dotnet new install` needs the version:
`dotnet new install LambdaWidgets.Templates::0.1.0-rc1000`. See [status](/status).
:::

Each one produces two projects and a dashboard file:

```
OrdersSearch/            the widget: handlers, views, and the data source behind an interface
OrdersSearch.Tests/      tests that drive it the way a viewer does
dashboard.json           a one-widget dashboard for the harness
README.md
```

Then two processes, and the loop is the same as any web application's:

```
dotnet run --project OrdersSearch          # the widget, under the AWS Lambda Test Tool
lambda-widgets --dashboard dashboard.json  # the console's side, on http://localhost:5080
```

## Options

| Option | Default | What it does |
|---|---|---|
| `-n`, `--name` | `MyWidget` | The project name, the namespace, and the class prefix. |
| `--tests` | `true` | `--tests false` leaves out the test project. |
| `--hardenedVersion` | `0.30.0-rc1000` | The Hardened.Framework version to reference. |
| `--widgetsVersion` | `0.1.0-rc1000` | The LambdaWidgets version to reference. |

The Lambda function name is `customWidget` followed by the project name, and it is also the
assembly name. That is what a deployment registers as the handler, and what the widget's endpoint
ARN ends with.

## What every template already does right

**The data source is behind an interface.** `ILogQueries`, `IItemLookups`, `IMetricSource` — one
per template, with a real implementation over the AWS SDK and a stand-in the tests substitute.
Mocking the SDK client instead would assert that the widget calls it the way the test imagines,
which is the implementation and is free to change. Substituting at this seam records what a
viewer's click actually asked for.

**Routes come from the generated links.**

```razor
@Widget.Button("Run query", Links.Pages.Search(), primary: true)
```

Rename `Search` and that line fails to compile. With a literal the widget would still render and
the button would silently do nothing, which is the failure custom widgets are worst at surfacing.
In a handler, where there is no `Links`, `OrdersSearchApp.Routes.Pages.Search()` is the same paths
as plain strings.

**The time range comes from the dashboard, never from a field.** A widget beside a graph covers the
window the viewer is looking at and follows them when they zoom it. A field for it would be a
second place for it to be wrong.

**The widget arrives configured and stays editable.** A dashboard author sets the log groups, the
table or the metric namespace in the widget's parameters; a viewer edits what they are allowed to.
The DynamoDB template will not let a click change the table, because a widget whose table a viewer
picks is a widget whose execution role has to allow every table.

**It publishes ahead of time.** `IsAotCompatible` is on, so a trim-unsafe call is a build error
rather than a widget that throws once it is deployed.

## The tests you get

```csharp
[HardenedTest]
public async Task WhatTheViewerTypedIsWhatIsSearched(IWidgetDriver widget, StandInLogQueries logs) {
    await widget.Open();
    widget.Fill("query", "stats count(*) by bin(5m)");

    await widget.Click("Run query");

    Assert.Equal("stats count(*) by bin(5m)", logs.Asked!.Value.Query);
}
```

Open, fill, click by the text on screen. No route, no payload field, no JSON — a viewer cannot name
any of those, and a test that did would break when the widget was rewritten without its behaviour
changing. See [testing a widget](/guide/testing-a-widget).

Every template also carries this one:

```csharp
[HardenedTest]
public async Task NothingTheWidgetRendersIsThrownAway(IWidgetDriver widget) =>
    Assert.Empty((await widget.Open()).Findings);
```

That is the [linter](/reference/linter) asserting the console would keep everything the widget
rendered. A stripped `onclick` or an action that binds nothing renders perfectly and then does
nothing at all.

## Deploying

```
dotnet publish OrdersSearch -c Release -r linux-arm64 -p:PublishAot=true
```

The handler is the assembly name. Add a custom widget to a dashboard with the function's ARN as its
endpoint, and give the execution role the one permission the template's README names. See
[deploying](/guide/deploying).
