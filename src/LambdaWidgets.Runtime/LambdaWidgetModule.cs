using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Aws.Lambda.Runtime.Adapters;
using Hardened.Aws.Lambda.Runtime.Modules;
using Hardened.Web.Runtime.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LambdaWidgets.Runtime;

/// <summary>
/// A CloudWatch custom widget application. Applied beside <c>[HardenedModule]</c>:
///
/// <code>
/// [HardenedModule]
/// [LambdaWidgetModule]
/// public partial class LogsSearch { }
///
/// public class Pages {
///     [Get("/")]
///     public LandingPage Index(IWidgetContext context) => new(context);
/// }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// <b>It composes the two modules a widget needs, so an author writes one attribute.</b>
/// <c>[HardenedWebModule]</c> brings the routing table and the web pipeline, because a widget's
/// pages are ordinary routes. <c>[LambdaRuntimeModule]</c> brings the invocation loop and the
/// request pipeline with it.
/// </para>
/// <para>
/// Composing the web module is not decoration. An application carrying <c>[HardenedModule]</c> and
/// this one alone registers no route table, and the failure is at the first invocation with "This
/// function declares no handlers" — which names neither the missing module nor this one. Day-one
/// check 7 found it, and this is where it is fixed rather than in every sample's application class.
/// </para>
/// </remarks>
[DependencyModule]
[HardenedWebModule]
[LambdaRuntimeModule]
public partial class LambdaWidgetModule : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) {
        services.AddSingleton<IPayloadAdapter, WidgetAdapter>();

        // One per sandbox, because one invocation runs at a time and the adapter writes it before
        // the scope opens.
        services.TryAddSingleton<WidgetContextAccessor>();

        // Scoped, so a handler takes IWidgetContext as a parameter and gets this invocation's.
        services.TryAddScoped(provider =>
            provider.GetRequiredService<WidgetContextAccessor>().Value);
    }
}
