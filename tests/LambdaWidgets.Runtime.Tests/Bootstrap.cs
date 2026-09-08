using Hardened.Shared.Testing.Attributes;
using LambdaWidgets.Runtime.Tests;

// A [HardenedTest] resolves its parameters from this application's container, so a test asks for
// LambdaInvocationHandler and drives the same loop a deployed widget runs under.
[assembly: HardenedTestEntryPoint(typeof(WidgetTestApp))]
