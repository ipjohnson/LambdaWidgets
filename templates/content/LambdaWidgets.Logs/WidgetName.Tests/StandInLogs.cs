using DependencyModules.Testing.Attributes.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WidgetName.Tests;

/// <summary>Logs Insights, standing still.</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StandInLogsAttribute : Attribute, ITestServiceSetupAttribute {
    public void SetupServiceCollection(
        ITestMethodContext testMethod, IServiceCollection serviceCollection) {
        serviceCollection.RemoveAll<ILogQueries>();
        serviceCollection.AddSingleton<StandInLogQueries>();
        serviceCollection.AddSingleton<ILogQueries>(
            provider => provider.GetRequiredService<StandInLogQueries>());
    }
}

/// <summary>What the widget asked for, and what it is told.</summary>
public sealed class StandInLogQueries : ILogQueries {
    /// <summary>The query the widget last ran, so a test can assert what the viewer's click meant.</summary>
    public (string LogGroups, string Query, DateTimeOffset Start, DateTimeOffset End, int Limit)? Asked
    { get; private set; }

    /// <summary>What the next run answers with.</summary>
    public LogResults Answer { get; set; } = new(
        [new LogRow([("@timestamp", "2026-09-08 09:00:00.000"), ("@message", "order placed")])],
        Scanned: 12_345,
        Status: "Complete");

    /// <summary>Thrown instead of answering, for the case a query fails.</summary>
    public Exception? Fails { get; set; }

    public Task<LogResults> Run(
        string logGroups,
        string query,
        DateTimeOffset start,
        DateTimeOffset end,
        int limit,
        CancellationToken cancellationToken) {
        Asked = (logGroups, query, start, end, limit);

        return Fails is not null ? Task.FromException<LogResults>(Fails) : Task.FromResult(Answer);
    }
}
