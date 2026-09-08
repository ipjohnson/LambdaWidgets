using Amazon.DynamoDBv2;
using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace WidgetName;

/// <summary>A DynamoDB lookup, as a widget.</summary>
[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class WidgetNameApp : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
}
