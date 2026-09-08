using DependencyModules.Testing.Attributes.Interfaces;
using LambdaWidgets.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LambdaWidgets.Testing;

/// <summary>
/// Makes <see cref="IWidgetDriver"/> resolvable in a widget application's tests.
///
/// <code>
/// [assembly: WidgetTesting]
/// [assembly: HardenedTestEntryPoint(typeof(LogsSearch))]
/// </code>
///
/// <code>
/// [HardenedTest]
/// public async Task ASearchRunsOverTheDashboardsRange(IWidgetDriver widget) {
///     widget.State = widget.State with { TimeRange = WidgetTimeRange.Relative(TimeSpan.FromHours(1)) };
///
///     await widget.Open();
///     widget.Fill("query", "fields @timestamp | limit 5");
///
///     var results = await widget.Click("Run query");
///
///     Assert.Contains("@timestamp", results.Html);
/// }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// It registers the interpreter and the driver, and nothing else. The application's own container
/// comes from <c>[HardenedTestEntryPoint]</c>, which is where <c>LambdaInvocationHandler</c> is
/// resolved from — so a driver test runs the widget's real invocation loop rather than a stand-in.
/// </para>
/// <para>
/// Every registration is <c>TryAdd</c>, so an application that has substituted a piece of the
/// interpreter keeps it under test. A test that ran against a different sanitizer than the
/// application ships would be asserting the wrong thing.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class WidgetTestingAttribute : Attribute, ITestServiceSetupAttribute {
    public void SetupServiceCollection(
        ITestMethodContext testMethod, IServiceCollection serviceCollection) {
        new DashboardModule().ConfigureServices(serviceCollection);

        serviceCollection.TryAddScoped<IWidgetDriver, WidgetDriver>();
    }
}
