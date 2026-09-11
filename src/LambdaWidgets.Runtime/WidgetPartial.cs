using Hardened.Requests.Abstract.Execution;
using Microsoft.Extensions.DependencyInjection;
using RazorBlade;
using RazorBlade.Support;

namespace LambdaWidgets.Runtime;

/// <summary>
/// A piece of a widget shared between its pages: a nav bar, a header, a row.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page cannot be a partial, and this is why there are two bases.</b> A view inheriting
/// <c>WidgetTemplates</c> is a response output: the pipeline constructs it, attaches the handler's
/// model and renders it, and the attach is <c>HardenedHtmlTemplate</c>'s own private business. So
/// there is no constructor to call and <c>Model</c> has no setter — a typed partial written against
/// that base gives <c>CS1729</c>, then <c>CS0200</c>, and a four-page widget ends up with its nav
/// bar copied into all four pages.
/// </para>
/// <para>
/// This is the other half: constructed by hand, from a page, with the model it should render.
/// <c>[TemplateConstructor]</c> is what makes RazorBlade's generator put a matching constructor on
/// the generated class.
/// </para>
/// <code>
/// @* Views/Nav.cshtml *@
/// @inherits LambdaWidgets.Runtime.WidgetPartial&lt;string&gt;
/// @Widget.Link("Board", Model)
/// </code>
/// <code>
/// @* Views/BoardPage.cshtml, and every other page *@
/// @(new Nav(Links.Board.Index(), Context))
/// </code>
/// <para>
/// <see cref="WidgetPartial{TModel,TLinks}"/> is the one to inherit when the partial links
/// anywhere, which a nav bar does.
/// </para>
/// </remarks>
public abstract class WidgetPartial<TModel> : HtmlTemplate {
    private WidgetHelpers? _widget;

    /// <remarks>
    /// <paramref name="context"/> is the page's own <c>Context</c>, which is protected on the
    /// template base and so reachable from any view. It is what <c>Widget</c> and <c>Links</c> are
    /// resolved from, and a partial has no other way to reach the container.
    /// </remarks>
    [TemplateConstructor]
    protected WidgetPartial(TModel model, IExecutionContext context) {
        Model = model;
        Context = context;
    }

    public TModel Model { get; }

    protected IExecutionContext Context { get; }

    /// <summary>The helpers, the same ones a page has.</summary>
    protected WidgetHelpers Widget =>
        _widget ??= new WidgetHelpers(Context.RequestServices.GetRequiredService<IWidgetContext>());
}

/// <summary>
/// A shared piece that links somewhere, which is what a nav bar is.
/// </summary>
/// <remarks>
/// <para>
/// <typeparamref name="TLinks"/> is the application's generated <c>Links</c> type, named by the
/// view rather than woven in by a generator:
/// </para>
/// <code>
/// @inherits LambdaWidgets.Runtime.WidgetPartial&lt;NavModel, DeliveryOps.Links&gt;
/// @Widget.Link("Board", Links.Board.Index())
/// </code>
/// <para>
/// <b>Every route still comes from the generated links.</b> That is the whole point of the base
/// taking the type rather than the partial building strings: renaming a handler breaks the nav bar
/// at its own line during the build.
/// </para>
/// </remarks>
public abstract class WidgetPartial<TModel, TLinks> : WidgetPartial<TModel> where TLinks : class {
    private TLinks? _links;

    [TemplateConstructor]
    protected WidgetPartial(TModel model, IExecutionContext context) : base(model, context) { }

    protected TLinks Links =>
        _links ??= Context.RequestServices.GetRequiredService<TLinks>();
}
