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

var provider = services.BuildServiceProvider();

// Startup services run here, and on the 0.30 line nothing else runs them. Without this the describe
// filter is never installed and the console's Get documentation button answers with the widget's
// landing page. Hardened.Aws.Lambda.Runtime 0.31.0-rc1000 does it inside
// HardenedLambdaBootstrap.Run, so this line comes out with that uptake; until then it is harmless
// on either line, because ApplicationLogic.Start runs a provider's startup services once.
// FINDINGS.md F-11.
await ApplicationLogic.Start(provider, null);

await HardenedLambdaBootstrap.Run(provider);
