using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;
using LambdaWidgets.Testing.Tests;

// [WidgetTesting] makes IWidgetDriver resolvable; the entry point is where the widget's own
// container - and its real invocation loop - comes from.
[assembly: WidgetTesting]
[assembly: HardenedTestEntryPoint(typeof(SearchWidget))]
