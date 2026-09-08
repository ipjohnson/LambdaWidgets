using DependencyModules.Runtime.Attributes;
using DependencyModules.Runtime.Interfaces;
using LambdaWidgets.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Hardened.Requests.Abstract.Templates;
using RazorBlade;

namespace LambdaWidgets.Charts;

/// <summary>
/// Charts for a widget, applied as <c>[ChartsModule]</c>.
///
/// <code>
/// [HardenedModule]
/// [LambdaWidgetModule]
/// [ChartsModule]
/// [Enable&lt;WidgetTemplates&gt;]
/// public partial class ErrorRates { }
/// </code>
/// </summary>
/// <remarks>
/// A package rather than a flag, which is the opt-in Hardened's own optional pieces use: a widget
/// that draws nothing carries none of this.
/// </remarks>
[DependencyModule]
public partial class ChartsModule : IServiceCollectionConfiguration {
    public void ConfigureServices(IServiceCollection services) =>
        // TryAdd, so a widget that has its own house style keeps it. The specs here are the ones
        // worth defaulting to, not the only ones anybody may hold.
        services.TryAddSingleton<IChartRenderer, SvgChartRenderer>();
}

/// <summary>
/// A widget view that draws.
/// </summary>
/// <remarks>
/// <see cref="WidgetTemplate{TModel}"/> with the renderer attached, so a view writes
/// <c>@Draw(chart)</c> and names neither the theme nor the endpoint — both come from the
/// invocation, and a template that named either would stop working on a dark dashboard or in a
/// second account.
/// </remarks>
public abstract class ChartTemplate<TModel> : WidgetTemplate<TModel> {
    private IChartRenderer? _renderer;
    private IWidgetContext? _widget;

    /// <summary>Writes a chart, resolved for the dashboard's own theme.</summary>
    /// <remarks>
    /// Null draws nothing, so a view can hold a chart the handler chose not to fill without
    /// wrapping every one of them in a condition.
    /// </remarks>
    protected IEncodedContent Draw(Chart? chart) {
        if (chart is null) {
            return new HtmlString("");
        }

        _renderer ??= Context.RequestServices.GetRequiredService<IChartRenderer>();
        _widget ??= Context.RequestServices.GetRequiredService<IWidgetContext>();

        return new HtmlString(_renderer.Render(
            chart,
            _widget.Theme == WidgetTheme.Dark ? ChartTheme.Dark : ChartTheme.Light,
            _widget.InvokedFunctionArn));
    }
}

/// <summary>
/// The marker a charting widget enables, which gives its views <c>@Draw</c> beside <c>@Widget</c>
/// and <c>@Links</c>.
///
/// <code>
/// [HardenedModule]
/// [LambdaWidgetModule]
/// [ChartsModule]
/// [Enable&lt;ChartTemplates&gt;]
/// public partial class ErrorRates { }
/// </code>
///
/// <code>
/// @inherits ErrorRatesChartTemplates&lt;ErrorPage&gt;
/// @Draw(Model.Errors)
/// </code>
/// </summary>
[TemplateBase(typeof(ChartTemplate<>))]
[TemplateContentType("text/html; charset=utf-8")]
public sealed class ChartTemplates { }
