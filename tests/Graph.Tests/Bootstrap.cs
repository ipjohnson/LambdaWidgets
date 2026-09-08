using Graph;
using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;

// The sample makes its own numbers up, so there is nothing to stand in for: the widget runs its
// real handlers, its real binding and its real views.
[assembly: WidgetTesting]
[assembly: HardenedTestEntryPoint(typeof(GraphApp))]
