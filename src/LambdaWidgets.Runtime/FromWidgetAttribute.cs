using Hardened.Requests.Abstract.Attributes;
using Hardened.Requests.Abstract.Execution;
using Hardened.Requests.Abstract.Serializer;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaWidgets.Runtime;

/// <summary>
/// Binds a handler's request object from everything the console sent.
///
/// <code>
/// [Get("/search")]
/// public ResultsPage Search([FromWidget] SearchRequest request, IWidgetContext context) { ... }
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// The values are <c>widgetContext.params</c>, the event's top-level fields and
/// <c>widgetContext.forms.all</c>, merged in that order so that what the viewer typed beats what an
/// action sent, which beats how the widget was configured. A handler binds one object and never has
/// to know which of the three a value came from.
/// </para>
/// <para>
/// <b>The attribute is not decoration, and the reason is a build error.</b> Without it the parameter
/// is a complex type with no other source, which Hardened infers as the body — and then
/// <c>HRDR010</c> refuses the build: "read from the request body, and a GET carries none, so a
/// request that sends no body is refused before the handler runs and the published document gives
/// the operation a body it should not have." Both halves of that are true of HTTP and neither is
/// true here. A widget's <c>GET</c> is a scheme label rather than a method, there is no client that
/// could omit a body, and a widget publishes no document. Naming a source of its own says so,
/// where suppressing the diagnostic would only silence it.
/// </para>
/// <para>
/// It binds through the framework's own deserializer rather than reflecting over
/// <typeparamref name="T" />, which is what keeps a widget publishable ahead of time and what makes
/// <c>"20"</c> arrive as an <c>int</c>: the deserializer defaults to
/// <c>JsonNumberHandling.AllowReadingFromString</c>, and a form field is always text.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromWidgetAttribute : Attribute, ICustomBindingAttribute {
    public ValueTask<T> BindValue<T>(IExecutionContext context, IExecutionRequestParameter parameter) =>
        Bind<T>(context);

    private static async ValueTask<T> Bind<T>(IExecutionContext context) {
        var deserializer = context.RequestServices
            .GetRequiredService<ISerializationLocatorService>()
            .FindRequestDeserializer(context);

        // The merged values, which WidgetRequest wrote as the body for exactly this.
        //
        // Never null in practice, and not defended against. WidgetRequest always writes an object,
        // so a widget on its landing page deserializes {} into an empty instance rather than into
        // nothing. Constructing a fallback would need Activator.CreateInstance<T>, and T cannot
        // carry the DynamicallyAccessedMembers annotation that needs because ICustomBindingAttribute
        // declares the signature - so the defence would cost IL2091 and buy a case that cannot
        // happen.
        return (await deserializer.DeserializeRequestBody<T>(context))!;
    }
}
