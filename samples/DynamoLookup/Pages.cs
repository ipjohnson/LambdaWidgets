using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace DynamoLookup;

/// <summary>
/// The widget's pages.
/// </summary>
/// <remarks>
/// An ordinary class, found by the route table the generator builds from the <c>[Get]</c>
/// attributes. A widget can have as many of these as it has areas; the application class is the
/// one place that composes modules, and it is deliberately somewhere else.
/// </remarks>
public class Pages(IItemLookups lookups) {

    /// <summary>The form. The table is the dashboard author's choice, the key is the viewer's.</summary>
    [Get("/")]
    [Output<Views.LandingPage>]
    public LookupPage Index([FromWidget] LookupRequest request, IWidgetContext context) =>
        new(Table(request, context), request.PartitionKey, request.SortPrefix, Page: null);

    /// <summary>
    /// Looks the key up, and carries a cursor if there is more.
    /// </summary>
    /// <remarks>
    /// <b>The cursor arrives in the request like any other value and leaves in the next action's
    /// fields.</b> That is the whole of paging here: nothing is remembered between invocations, so
    /// the page the viewer is on is a property of the link they are about to click rather than of
    /// anything on the server.
    /// </remarks>
    [Get("/look-up")]
    [Output<Views.ResultsPage>]
    public async Task<LookupPage> LookUp(
        [FromWidget] LookupRequest request,
        IWidgetContext context,
        CancellationToken cancellationToken) {
        var table = Table(request, context);

        var page = await lookups.Query(
            table, request.PartitionKey, request.SortPrefix, request.Cursor, request.Limit,
            cancellationToken);

        return new LookupPage(table, request.PartitionKey, request.SortPrefix, page);
    }

    /// <remarks>
    /// The configured table wins over anything in the request. A widget whose table a viewer could
    /// change by clicking is a widget whose execution role has to allow every table, which is the
    /// opposite of what scoping the role is for.
    /// </remarks>
    private static string Table(LookupRequest request, IWidgetContext context) =>
        context.Params.TryGetValue("table", out var configured) && configured.Length > 0
            ? configured
            : request.Table;
}

/// <summary>What the viewer is looking for.</summary>
public class LookupRequest {
    public string Table { get; set; } = "";

    public string PartitionKey { get; set; } = "";

    /// <summary>Optional. Narrows the sort key with <c>begins_with</c>.</summary>
    public string SortPrefix { get; set; } = "";

    /// <summary>Where the previous page stopped. Empty on the first.</summary>
    public string Cursor { get; set; } = "";

    public int Limit { get; set; } = 10;
}

/// <summary>The model both views render.</summary>
public record LookupPage(string Table, string PartitionKey, string SortPrefix, ItemPage? Page);
