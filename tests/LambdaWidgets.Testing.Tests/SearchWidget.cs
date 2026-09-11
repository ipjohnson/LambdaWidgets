using Hardened.Requests.Abstract.Attributes;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace LambdaWidgets.Testing.Tests;

/// <summary>
/// A widget with the things a driver has to be able to work: a form, buttons, a confirmation, a
/// popup, and more than one page.
/// </summary>
/// <remarks>
/// Shaped like the Logs Insights sample rather than invented, so the driver is exercised against
/// the kind of widget it exists for.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class SearchWidget { }

public class Pages {
    [Get("/")]
    [Output<Views.LandingPage>]
    public SearchPage Index([FromWidget] SearchRequest request) =>
        new(request.Query is { Length: > 0 } ? request.Query : "fields @timestamp", Results: null);

    [Get("/search")]
    [Output<Views.ResultsPage>]
    public SearchPage Search([FromWidget] SearchRequest request) =>
        new(request.Query, Results: $"{request.Limit} row(s) for {request.Query}");

    [Get("/reset")]
    [Output<Views.LandingPage>]
    public SearchPage Reset() => new("fields @timestamp", Results: null);

    /// <summary>A query that was throttled, which is the ordinary way a real widget fails.</summary>
    [Get("/throttled")]
    public string Throttled() =>
        throw new InvalidOperationException("Throughput exceeded on table deliveries");
}

public class SearchRequest {
    public string Query { get; set; } = "";

    public int Limit { get; set; } = 20;
}

public record SearchPage(string Query, string? Results);
