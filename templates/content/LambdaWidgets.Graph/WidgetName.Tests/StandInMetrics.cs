using DependencyModules.Testing.Attributes.Interfaces;
using LambdaWidgets.Charts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WidgetName.Tests;

/// <summary>CloudWatch, standing still.</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StandInMetricsAttribute : Attribute, ITestServiceSetupAttribute {
    public void SetupServiceCollection(
        ITestMethodContext testMethod, IServiceCollection serviceCollection) {
        serviceCollection.RemoveAll<IMetricSource>();
        serviceCollection.AddSingleton<StandInMetricSource>();
        serviceCollection.AddSingleton<IMetricSource>(
            provider => provider.GetRequiredService<StandInMetricSource>());
    }
}

/// <summary>Answers a fixed shape, and remembers what it was asked.</summary>
public sealed class StandInMetricSource : IMetricSource {
    /// <summary>What the widget last asked for, so a test can assert on the dashboard's controls.</summary>
    public (string Namespace, IReadOnlyList<string> Metrics, DateTimeOffset Start, DateTimeOffset End,
        int PeriodSeconds)? Asked
    { get; private set; }

    /// <summary>How many readings each series carries.</summary>
    public int Points { get; set; } = 12;

    public Task<IReadOnlyList<Series>> Over(
        string namespaceName,
        IReadOnlyList<string> metricNames,
        DateTimeOffset start,
        DateTimeOffset end,
        int periodSeconds,
        CancellationToken cancellationToken) {
        Asked = (namespaceName, metricNames, start, end, periodSeconds);

        var step = (end - start) / Math.Max(1, Points);

        // Distinct totals per metric, so a test can tell the columns apart by their heights.
        return Task.FromResult<IReadOnlyList<Series>>(metricNames
            .Select((name, m) => new Series(
                name,
                Enumerable.Range(0, Points)
                    .Select(i => new Reading(start + step * i, (m + 1) * 10 + i))
                    .ToList()))
            .ToList());
    }
}
