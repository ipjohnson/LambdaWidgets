using DynamoLookup;
using DynamoLookup.Tests;
using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Testing;

[assembly: WidgetTesting]
[assembly: StandInTable]
[assembly: HardenedTestEntryPoint(typeof(DynamoLookupApp))]
