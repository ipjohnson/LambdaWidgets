using DependencyModules.Runtime.Attributes;
using Hardened.Requests.Abstract.Attributes;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using Hardened.Web.Kestrel.Runtime;
using Hardened.Web.Runtime.DependencyInjection;
using Hardened.Web.StaticContent;
using LambdaWidgets.Dashboard;

namespace LambdaWidgets.Harness;

/// <summary>
/// <c>lambda-widgets</c>: a console-shaped dashboard on your machine.
/// </summary>
/// <remarks>
/// A Hardened application on the Kestrel host, so the proof runs on both of Hardened's hosts in one
/// repository — a widget on the Lambda host and the thing that develops it on the web host.
/// </remarks>
[HardenedModule]
[KestrelRuntime]
[DashboardModule]
[HardenedStaticContent]
[Enable<Hardened.Templates.RazorBlade.RazorTemplates>]
public partial class HarnessApp { }

/// <summary>
/// What the page calls. Everything a widget does is <see cref="IWidgetHarness"/>'s; these turn one
/// viewer gesture into one call and hand back what to render.
/// </summary>
/// <remarks>
/// <para>
/// <b>The page holds no rules.</b> Its JavaScript reports which bound element was clicked and what
/// is in the form fields, and nothing else — every decision about what that means is server side,
/// in the interpreter both the harness and the test driver resolve. A page that decided anything
/// would be a second implementation of the console, and the two would drift.
/// </para>
/// <para>
/// The formal HTTP API of plan 7.3, for driving a click-through from Python or Node, is item 9.
/// These are the endpoints the page needs and no more.
/// </para>
/// </remarks>
[Handler]
public class HarnessRoutes(IWidgetHarness harness) {

    [Get("/")]
    [Output<Views.DashboardPage>]
    public DashboardPage Index() => new(harness);

    [Get("/widgets/{id}")]
    [Output<Views.WidgetPanel>]
    public async Task<WidgetPanel> Open(string id, CancellationToken cancellationToken) =>
        new(id, await harness.Open(id, cancellationToken));

    /// <param name="index">Which of the shown widget's actions the viewer fired.</param>
    /// <param name="fields">
    /// What is in the widget's form fields now, as the page read them out of the DOM. Anything the
    /// viewer did not touch keeps what the function rendered, which the interpreter works out.
    /// </param>
    [Post("/widgets/{id}/actions/{index}")]
    [Output<Views.WidgetPanel>]
    public async Task<WidgetPanel> Click(
        string id,
        int index,
        [FromBody] Dictionary<string, string> fields,
        CancellationToken cancellationToken) =>
        new(id, await harness.Click(id, index, fields, cancellationToken));

    [Post("/widgets/{id}/describe")]
    [Output<Views.WidgetPanel>]
    public async Task<WidgetPanel> Describe(string id, CancellationToken cancellationToken) =>
        new(id, await harness.Describe(id, cancellationToken));

    /// <summary>
    /// A dashboard control moved, so every widget that asked to hear about it is re-invoked.
    /// </summary>
    [Post("/refresh")]
    [Output<Views.DashboardPage>]
    public async Task<DashboardPage> Refresh(CancellationToken cancellationToken) {
        await harness.RefreshAll(DashboardTrigger.Refresh, cancellationToken);

        return new DashboardPage(harness);
    }
}

/// <summary>The whole dashboard.</summary>
public record DashboardPage(IWidgetHarness Harness);

/// <summary>One widget, and what it last answered.</summary>
public record WidgetPanel(string Id, WidgetView View);
