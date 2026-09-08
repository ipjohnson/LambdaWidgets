using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace WidgetName;

/// <summary>
/// The widget's pages.
/// </summary>
/// <remarks>
/// An ordinary class, found by the route table the generator builds from the <c>[Get]</c>
/// attributes. A widget can have as many of these as it has areas; the application class is the
/// one place that composes modules, and it lives in its own file for that reason.
/// </remarks>
public class Pages(ILogQueries queries) {

    /// <summary>
    /// The form, prefilled from the widget's configured parameters.
    /// </summary>
    /// <remarks>
    /// A dashboard author configures the log groups and a starting query, and a viewer edits them.
    /// That is the division the console's parameters are for: the widget arrives useful and stays
    /// editable.
    /// </remarks>
    [Get("/")]
    [Output<Views.LandingPage>]
    public SearchPage Index([FromWidget] SearchRequest request, IWidgetContext context) =>
        new(Query(request, context), LogGroups(request, context), context, Results: null);

    /// <summary>
    /// Runs the query over the dashboard's effective time range.
    /// </summary>
    /// <remarks>
    /// <b>The range comes from the context, not from a field.</b> A widget beside a graph should
    /// search the window the viewer is looking at, and follow them when they zoom it. Asking for
    /// the range in the form would be a second place for it to be wrong.
    /// </remarks>
    [Get("/search")]
    [Output<Views.ResultsPage>]
    public async Task<SearchPage> Search(
        [FromWidget] SearchRequest request,
        IWidgetContext context,
        CancellationToken cancellationToken) {
        var (start, end) = context.TimeRange.Effective;
        var groups = LogGroups(request, context);
        var query = Query(request, context);

        var results = await queries.Run(groups, query, start, end, request.Limit, cancellationToken);

        return new SearchPage(query, groups, context, results);
    }

    /// <summary>The query the dashboard author configured, whatever the viewer has typed since.</summary>
    [Get("/reset")]
    [Output<Views.LandingPage>]
    public SearchPage Reset(IWidgetContext context) =>
        new(Configured(context, "query", DefaultQuery), Configured(context, "logGroups", ""),
            context, Results: null);

    private const string DefaultQuery =
        "fields @timestamp, @message\n| sort @timestamp desc\n| limit 20";

    private static string Query(SearchRequest request, IWidgetContext context) =>
        string.IsNullOrWhiteSpace(request.Query)
            ? Configured(context, "query", DefaultQuery)
            : request.Query;

    private static string LogGroups(SearchRequest request, IWidgetContext context) =>
        string.IsNullOrWhiteSpace(request.LogGroups)
            ? Configured(context, "logGroups", "")
            : request.LogGroups;

    private static string Configured(IWidgetContext context, string name, string fallback) =>
        context.Params.TryGetValue(name, out var value) && value.Length > 0 ? value : fallback;
}

/// <summary>What the viewer typed, or what the dashboard configured.</summary>
public class SearchRequest {
    /// <summary>Comma separated, which is how the console's own sample carries them.</summary>
    public string LogGroups { get; set; } = "";

    public string Query { get; set; } = "";

    public int Limit { get; set; } = 20;
}

/// <summary>The model both views render.</summary>
public record SearchPage(
    string Query,
    string LogGroups,
    IWidgetContext Context,
    LogResults? Results);
