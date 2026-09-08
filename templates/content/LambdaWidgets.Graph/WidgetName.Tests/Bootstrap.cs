using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;
using WidgetName;
using WidgetName.Tests;

// WidgetTesting registers the click-through driver. The stand-in replaces the data source at its
// own interface, so the widget runs its real handlers, its real binding and its real views.
[assembly: WidgetTesting]
[assembly: StandInMetrics]
[assembly: HardenedTestEntryPoint(typeof(WidgetNameApp))]
