using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;
using LogsSearch;
using LogsSearch.Tests;

// StandInLogs replaces the CloudWatch client, so the widget runs its real handlers, its real
// binding and its real views against a query engine that answers instantly.
[assembly: WidgetTesting]
[assembly: StandInLogs]
[assembly: HardenedTestEntryPoint(typeof(LogsSearchApp))]
