using Amazon.CloudWatch;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Charts;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace WidgetName;

/// <summary>Charting a data source, as a widget.</summary>
[HardenedModule]
[LambdaWidgetModule]
[ChartsModule]
[Enable<ChartTemplates>]
public partial class WidgetNameApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAmazonCloudWatch>(_ => new AmazonCloudWatchClient());
}
