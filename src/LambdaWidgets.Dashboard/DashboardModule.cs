using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LambdaWidgets.Dashboard;

/// <summary>
/// The console's rules, as services. Applied to an application as <c>[DashboardModule]</c>.
///
/// <code>
/// [HardenedModule]
/// [HardenedWebModule]
/// [DashboardModule]
/// public partial class Harness { }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// The harness resolves <see cref="IWidgetConsole"/> and so does the test driver, which is what
/// stops the two from disagreeing about what a click sends. Neither constructs one: a driver that
/// newed up its own would be free to compose the pieces differently, and the drift this library
/// exists to prevent would be back.
/// </para>
/// <para>
/// <b>Every registration is <c>TryAdd</c>.</b> The pieces are the parts most worth substituting —
/// a stricter sanitizer once day-one check 3 says what the console really strips, a stylesheet
/// measured rather than estimated — and an application that has registered its own should keep it
/// however the modules were ordered.
/// </para>
/// </remarks>
[DependencyModule]
public partial class DashboardModule : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) {
        services.TryAddSingleton<IWidgetActions, WidgetActions>();
        services.TryAddSingleton<IWidgetForms, WidgetForms>();
        services.TryAddSingleton<IWidgetSanitizer, WidgetSanitizer>();
        services.TryAddSingleton<IWidgetStyles, WidgetStyles>();
        services.TryAddSingleton<IWidgetResponses, WidgetResponses>();
        services.TryAddSingleton<IWidgetEvents, WidgetEvents>();
        services.TryAddSingleton<IDashboardBodies, DashboardBodies>();
        services.TryAddSingleton<IWidgetLinter, WidgetLinter>();

        services.TryAddSingleton<IWidgetConsole, WidgetConsole>();
    }
}
