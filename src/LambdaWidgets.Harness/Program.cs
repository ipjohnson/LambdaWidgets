using Hardened.Requests.Abstract.Errors;
using Hardened.Shared.Runtime.Application;
using Hardened.Web.Kestrel.Runtime;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Harness;
using LambdaWidgets.Harness.Invoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Answered before anything is built, and answered by exiting.
if (CommandLine.WantsHelp(args)) {
    Console.WriteLine(CommandLine.Usage);

    return 0;
}

if (CommandLine.WantsVersion(args)) {
    Console.WriteLine(CommandLine.Version);

    return 0;
}

// The dashboard file is the artifact: the harness renders it, editing a widget writes it back, and
// section 10 deploys the same file as the dashboard.
var declared = CommandLine.Dashboard(args);
var path = declared ?? "dashboard.json";

if (declared is not null && !File.Exists(declared)) {
    Console.Error.WriteLine($"No dashboard file at '{declared}'.");

    return 1;
}

var body = File.Exists(path) ? File.ReadAllText(path) : """{"widgets":[]}""";

var routing = InvokeRouting.From(args);

// Listens on 5080, the same address the Kestrel host uses everywhere else. Override with PORT.
var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var configured) ? configured : 5080;

var services = new ServiceCollection();

services.AddLogging(logging => logging.AddSimpleConsole(options => options.SingleLine = true));
services.AddHardenedEnvironment(new EnvironmentImpl(arguments: args));

// Ahead of the module, because the framework registers its own with TryAdd and first wins. Without
// this the harness's own messages - which name the widget ids a dashboard has, or say how to make
// one - reach the log and the caller gets "The server could not complete this request."
services.AddSingleton<IExceptionToModelConverter, HarnessErrors>();

// Resolved rather than constructed per invoke: one client pools its connections, and a new one per
// click exhausts sockets on a dashboard that auto-refreshes.
services.AddSingleton(new HttpClient());
services.AddSingleton(routing);
services.AddSingleton(new DashboardBodies().Read(body));

new HarnessApp().PopulateServiceCollection(services);

await using var app = HardenedKestrelApplication.Create(services, kestrel => kestrel.ListenAnyIP(port));

try {
    await app.StartAsync();
}
catch (Exception failure) when (CommandLine.IsPortInUse(failure)) {
    // Kestrel's own answer is a 25-line stack trace naming an address, which is the first thing a
    // second `lambda-widgets` in the same directory gets.
    Console.Error.WriteLine(
        $"Port {port} is in use. Stop what is listening on it, or set PORT to another.");

    return 1;
}

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("lambda-widgets");

logger.LogInformation("Dashboard {Path}, {Count} widget(s)", path, app.Services
    .GetRequiredService<IWidgetHarness>().Dashboard.Widgets.Count);
logger.LogInformation("Invokes go to {Target}", routing.Fallback.Description);
logger.LogInformation("Browse http://localhost:{Port}", port);

await app.RunAsync();

return 0;
