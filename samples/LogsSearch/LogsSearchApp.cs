using Amazon.CloudWatchLogs;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace LogsSearch;

/// <summary>
/// A Logs Insights search, as a widget.
/// </summary>
/// <remarks>
/// The sample that proves the parts Echo does not: a form the viewer types into, the dashboard's
/// time range reaching a real query, an AWS SDK client under ahead-of-time compilation, and a call
/// slow enough that the console waits on it.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class LogsSearchApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        // The default credential chain and the region from the environment, which is what a
        // deployed function has and what `dotnet run` picks up from a developer's profile.
        services.AddSingleton<IAmazonCloudWatchLogs>(_ => new AmazonCloudWatchLogsClient());
}
