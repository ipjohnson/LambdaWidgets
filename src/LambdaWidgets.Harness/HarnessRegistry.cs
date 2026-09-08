using System.Collections.Concurrent;
using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Dashboard;
using LambdaWidgets.Harness.Invoke;

namespace LambdaWidgets.Harness;

/// <summary>
/// Dashboards the API is driving, beside the one loaded from a file.
/// </summary>
/// <remarks>
/// <para>
/// A test in another language posts its own dashboard rather than editing the file the page is
/// showing, so it gets its own state, its own widgets and its own history. Two tests running at
/// once do not collide, and neither disturbs the page a developer has open.
/// </para>
/// <para>
/// In memory and not persisted. A harness run is a development session; a dashboard that outlived
/// it would be a second place for a stale one to hide.
/// </para>
/// </remarks>
public interface IHarnessRegistry {
    /// <summary>The dashboard loaded from the file, which the page shows.</summary>
    IWidgetHarness Default { get; }

    /// <summary>Takes a dashboard body and answers the id to drive it by.</summary>
    string Add(string dashboardBody);

    /// <summary>The dashboard with this id, or the default one for the reserved id.</summary>
    /// <exception cref="KeyNotFoundException">No dashboard has that id.</exception>
    IWidgetHarness Get(string id);
}

/// <inheritdoc />
[SingletonService(As = typeof(IHarnessRegistry))]
public sealed class HarnessRegistry : IHarnessRegistry {
    /// <summary>The id the file-loaded dashboard answers to, so the page and the API share one.</summary>
    public const string DefaultId = "default";

    private readonly IWidgetConsole _console;
    private readonly IWidgetInvoker _invoker;
    private readonly IDashboardBodies _bodies;
    private readonly ConcurrentDictionary<string, IWidgetHarness> _dashboards = new(StringComparer.Ordinal);

    public HarnessRegistry(
        IWidgetConsole console,
        IWidgetInvoker invoker,
        IDashboardBodies bodies,
        IWidgetHarness loaded) {
        _console = console;
        _invoker = invoker;
        _bodies = bodies;
        Default = loaded;

        _dashboards[DefaultId] = loaded;
    }

    public IWidgetHarness Default { get; }

    public string Add(string dashboardBody) {
        var id = Guid.NewGuid().ToString("n")[..12];

        _dashboards[id] = new WidgetHarness(_console, _invoker, _bodies.Read(dashboardBody));

        return id;
    }

    public IWidgetHarness Get(string id) =>
        _dashboards.TryGetValue(id, out var harness)
            ? harness
            : throw new KeyNotFoundException(
                $"No dashboard '{id}'. POST /api/dashboards with a dashboard body to make one, or " +
                $"use '{DefaultId}' for the one loaded from the file.");
}
