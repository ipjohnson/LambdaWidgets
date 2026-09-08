using DependencyModules.Testing.Attributes.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DynamoLookup.Tests;

/// <summary>A table with two pages in it, standing still.</summary>
/// <remarks>
/// Substituted at <see cref="IItemLookups"/> rather than at the SDK client, so the tests record
/// what a viewer's lookup asked for rather than how DynamoDB was called. The real implementation is
/// what proves the SDK publishes ahead of time, and that is checked by publishing.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class StandInTableAttribute : Attribute, ITestServiceSetupAttribute {
    public void SetupServiceCollection(
        ITestMethodContext testMethod, IServiceCollection serviceCollection) {
        serviceCollection.RemoveAll<IItemLookups>();
        serviceCollection.AddSingleton<StandInLookups>();
        serviceCollection.AddSingleton<IItemLookups>(
            provider => provider.GetRequiredService<StandInLookups>());
    }
}

/// <summary>Answers two pages, and remembers what it was asked.</summary>
public sealed class StandInLookups : IItemLookups {
    /// <summary>What the widget last asked for, including the cursor it carried.</summary>
    public (string Table, string PartitionKey, string SortPrefix, string Cursor, int Limit)? Asked
    { get; private set; }

    /// <summary>Every lookup, so a test can follow a viewer through the pages.</summary>
    public List<string> Cursors { get; } = [];

    public Task<ItemPage> Query(
        string table,
        string partitionKey,
        string sortPrefix,
        string cursor,
        int limit,
        CancellationToken cancellationToken) {
        Asked = (table, partitionKey, sortPrefix, cursor, limit);
        Cursors.Add(cursor);

        // The first page has more; the second is the end. That is the whole shape paging has to
        // handle, and it is what lets a test assert the Next link appears and then stops.
        return Task.FromResult(cursor.Length == 0
            ? new ItemPage(
                [new Item([("pk", partitionKey), ("sk", "a-1"), ("status", "placed")])],
                Cursor: "cursor-to-page-2")
            : new ItemPage(
                [new Item([("pk", partitionKey), ("sk", "a-2"), ("status", "shipped")])],
                Cursor: ""));
    }
}
