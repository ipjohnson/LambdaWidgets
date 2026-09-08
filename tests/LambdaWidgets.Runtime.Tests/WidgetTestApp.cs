using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace LambdaWidgets.Runtime.Tests;

/// <summary>
/// A widget application, written the way the guide tells an author to write one.
///
/// <para>
/// Two attributes, because <c>[LambdaWidgetModule]</c> composes the web module and the Lambda
/// runtime module. If that composition were ever removed this fixture would stop dispatching, which
/// is the point of writing it the documented way rather than the working way.
/// </para>
/// </summary>
[HardenedModule]
[LambdaWidgetModule]
public partial class WidgetTestApp { }

public class Pages {
    [Get("/")]
    public string Index() => "<h1>Log search</h1>";

    /// <summary>Echoes what it bound, so a test can see what reached a handler.</summary>
    [Get("/search")]
    public string Search([FromWidget] SearchRequest request) =>
        $"<p>{request.LogGroups}|{request.Query}|{request.Limit}</p>";

    [Get("/rows/{id}")]
    public string Row(string id) => $"<p>row {id}</p>";

    /// <summary>Reads the dashboard rather than the request, which is what IWidgetContext is for.</summary>
    [Get("/context")]
    public string Context(IWidgetContext context) =>
        $"<p>{context.DashboardName}|{context.Theme}|{context.WidgetId}|" +
        $"{context.TimeRange.Effective.Start:O}|{context.InvokedFunctionArn}</p>";

    [Get("/boom")]
    public string Boom() => throw new InvalidOperationException("the widget was not ready");
}

public class SearchRequest {
    public string LogGroups { get; set; } = "";

    public string Query { get; set; } = "";

    public int Limit { get; set; }
}
