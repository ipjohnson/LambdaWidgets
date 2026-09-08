using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DependencyModules.Runtime.Attributes;

namespace WidgetName;

/// <summary>One item, as named fields.</summary>
public sealed record Item(IReadOnlyList<(string Field, string Value)> Fields);

/// <summary>
/// A page of items, and how to ask for the next one.
/// </summary>
/// <param name="Cursor">
/// Empty when there is nothing more. Opaque on purpose: the widget carries it from one invocation
/// to the next without reading it, which is the whole of paging without a session.
/// </param>
public sealed record ItemPage(IReadOnlyList<Item> Items, string Cursor) {
    public bool HasMore => Cursor.Length > 0;
}

/// <summary>
/// Looking items up by key.
/// </summary>
/// <remarks>
/// <b>This interface is the seam a test substitutes.</b> It records what a viewer's lookup asked
/// for rather than how the SDK was called, so the tests keep passing when the query changes shape.
/// </remarks>
public interface IItemLookups {
    Task<ItemPage> Query(
        string table,
        string partitionKey,
        string sortPrefix,
        string cursor,
        int limit,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
[SingletonService(As = typeof(IItemLookups))]
public sealed class DynamoItemLookups(IAmazonDynamoDB dynamo) : IItemLookups {

    /// <remarks>
    /// The key attributes are named <c>pk</c> and <c>sk</c>. Change them to your table's, or read
    /// them from the widget's parameters if one widget has to serve several tables.
    /// </remarks>
    public async Task<ItemPage> Query(
        string table,
        string partitionKey,
        string sortPrefix,
        string cursor,
        int limit,
        CancellationToken cancellationToken) {
        var request = new QueryRequest {
            TableName = table,
            KeyConditionExpression = sortPrefix.Length > 0
                ? "#pk = :pk AND begins_with(#sk, :sk)"
                : "#pk = :pk",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#pk"] = "pk" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                [":pk"] = new() { S = partitionKey }
            },
            Limit = limit
        };

        if (sortPrefix.Length > 0) {
            request.ExpressionAttributeNames["#sk"] = "sk";
            request.ExpressionAttributeValues[":sk"] = new AttributeValue { S = sortPrefix };
        }

        if (cursor.Length > 0) {
            request.ExclusiveStartKey = Decode(cursor);
        }

        var response = await dynamo.QueryAsync(request, cancellationToken);

        return new ItemPage(
            response.Items.Select(Read).ToList(),
            response.LastEvaluatedKey is { Count: > 0 } last ? Encode(last) : "");
    }

    private static Item Read(Dictionary<string, AttributeValue> item) =>
        new(item.Select(field => (field.Key, Scalar(field.Value))).ToList());

    /// <remarks>
    /// The scalar types a viewer can read, and a type name for anything else. A widget showing
    /// "(list)" where a list is tells the viewer more than a serialized one they cannot take in.
    /// </remarks>
    private static string Scalar(AttributeValue value) =>
        value.S ?? value.N ??
        (value.IsBOOLSet ? value.BOOL.ToString().ToLowerInvariant() : null) ??
        (value.NULL ? "" : null) ??
        (value.SS?.Count > 0 ? string.Join(", ", value.SS) : null) ??
        (value.IsLSet ? "(list)" : null) ??
        (value.IsMSet ? "(map)" : null) ??
        "";

    /// <summary>
    /// The last evaluated key, small enough to ride in a <c>cwdb-action</c>'s JSON.
    /// </summary>
    /// <remarks>
    /// Base64 of JSON, and opaque to the widget. The encoding exists so the value survives a round
    /// trip through an HTML attribute and a JSON object without anybody escaping it twice. Only the
    /// scalar forms a key can take are carried, because a DynamoDB key is a string, a number or
    /// binary and nothing else can appear here.
    /// </remarks>
    private static string Encode(Dictionary<string, AttributeValue> key) {
        var flat = key.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.S is not null ? "S:" + entry.Value.S : "N:" + entry.Value.N);

        return Convert.ToBase64String(
            JsonSerializer.SerializeToUtf8Bytes(flat, ItemLookupJson.Default.DictionaryStringString));
    }

    private static Dictionary<string, AttributeValue> Decode(string cursor) {
        var flat = JsonSerializer.Deserialize(
            Convert.FromBase64String(cursor), ItemLookupJson.Default.DictionaryStringString) ?? new();

        return flat.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.StartsWith("S:", StringComparison.Ordinal)
                ? new AttributeValue { S = entry.Value[2..] }
                : new AttributeValue { N = entry.Value[2..] });
    }
}

/// <summary>
/// The cursor's serializer, source generated.
/// </summary>
/// <remarks>
/// Reflection-based serialization carries <c>RequiresDynamicCode</c>, which this project reports as
/// an error because it publishes ahead of time. A context is how you serialize at all here.
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class ItemLookupJson : JsonSerializerContext { }
