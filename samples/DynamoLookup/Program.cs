using DynamoLookup;
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
using var emulator = await LambdaEmulator.StartIfLocal(typeof(DynamoLookupApp));

var services = new ServiceCollection();

services.AddLogging(logging => logging.AddSimpleConsole(options => options.SingleLine = true));
services.AddTransient<IHardenedEnvironment>(_ => new EnvironmentImpl(arguments: args));

new DynamoLookupApp().PopulateServiceCollection(services);

var provider = services.BuildServiceProvider();

// Startup services run here, and nothing else runs them. HardenedLambdaBootstrap.Run resolves the
// invocation handler and serves the loop; every other Hardened host starts the application first.
// Without this the describe filter is never installed and the console's Get documentation button
// answers with the widget's landing page. FINDINGS.md F-11.
await ApplicationLogic.Start(provider, null);

await HardenedLambdaBootstrap.Run(provider);
