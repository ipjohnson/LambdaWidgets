using Hardened.Shared.Runtime.Application;
using Hardened.Web.Kestrel.Runtime;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Harness;
using LambdaWidgets.Harness.Invoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// The dashboard file is the artifact: the harness renders it, editing a widget writes it back, and
// section 10 deploys the same file as the dashboard. A run with no file gets an empty one rather
// than a failure, so `lambda-widgets` in an empty directory still starts and says so.
var path = Argument(args, "--dashboard") ?? "dashboard.json";
var body = File.Exists(path) ? File.ReadAllText(path) : """{"widgets":[]}""";

var routing = InvokeRouting.From(args);

// Listens on 5080, the same address the Kestrel host uses everywhere else. Override with PORT.
var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var configured) ? configured : 5080;

var services = new ServiceCollection();

services.AddLogging(logging => logging.AddSimpleConsole(options => options.SingleLine = true));
services.AddHardenedEnvironment(new EnvironmentImpl(arguments: args));

// Resolved rather than constructed per invoke: one client pools its connections, and a new one per
// click exhausts sockets on a dashboard that auto-refreshes.
services.AddSingleton(new HttpClient());
services.AddSingleton(routing);
services.AddSingleton(new DashboardBodies().Read(body));

new HarnessApp().PopulateServiceCollection(services);

await using var app = HardenedKestrelApplication.Create(services, kestrel => kestrel.ListenAnyIP(port));

await app.StartAsync();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("lambda-widgets");

logger.LogInformation("Dashboard {Path}, {Count} widget(s)", path, app.Services
    .GetRequiredService<IWidgetHarness>().Dashboard.Widgets.Count);
logger.LogInformation("Invokes go to {Target}", routing.Fallback.Description);
logger.LogInformation("Browse http://localhost:{Port}", port);

await app.RunAsync();

static string? Argument(string[] arguments, string name) {
    var at = Array.IndexOf(arguments, name);

    return at >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
}
