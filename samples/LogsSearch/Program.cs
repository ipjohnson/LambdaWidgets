using LogsSearch;
using Hardened.Aws.Lambda.Runtime.Development;
using Hardened.Aws.Lambda.Runtime.Hosting;
using Hardened.Shared.Runtime.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Started by the Lambda service, this does nothing and the bootstrap reads the address the service
// set. Started with `dotnet run`, there is no such address, so this brings up the AWS Lambda Test
// Tool and sets the same variable the service would have - which is what puts a widget under a
// debugger with no second project.
//
// apiGateway: false, the default. The tool's gateway emulator fronts an HTTP application, and a
// widget is invoked rather than requested.
using var emulator = await LambdaEmulator.StartIfLocal(typeof(LogsSearchApp));

var services = new ServiceCollection();

services.AddLogging(logging => logging.AddSimpleConsole(options => options.SingleLine = true));
services.AddTransient<IHardenedEnvironment>(_ => new EnvironmentImpl(arguments: args));

new LogsSearchApp().PopulateServiceCollection(services);

await HardenedLambdaBootstrap.Run(services.BuildServiceProvider());
