using Amazon.CloudWatchLogs;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace WidgetName;

/// <summary>A Logs Insights search, as a widget.</summary>
[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class WidgetNameApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        // The default credential chain and the region from the environment, which is what a
        // deployed function has and what `dotnet run` picks up from a developer's profile.
        services.AddSingleton<IAmazonCloudWatchLogs>(_ => new AmazonCloudWatchLogsClient());
}
