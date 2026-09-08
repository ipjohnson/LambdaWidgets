using System.Text.Json;
using Amazon.Lambda.Core;
using Hardened.Aws.Lambda.Runtime.Adapters;
using Hardened.Aws.Lambda.Runtime.Execution;
using Hardened.Requests.Abstract.Execution;

namespace LambdaWidgets.Runtime;

/// <summary>
/// A CloudWatch custom widget invocation: the console's event in, a document out.
/// </summary>
/// <remarks>
/// <para>
/// Web-shaped in, invoke-shaped out, and the asymmetry is the design. The handler sees a path and a
/// query string, because a widget's pages are ordinary routes; the caller receives exactly what the
/// handler returned, because the console renders the function's return value with no envelope
/// around it.
/// </para>
/// <para>
/// <c>ApiGatewayAdapter</c> is the model for the first half and <c>InvokeAdapter</c> for the second.
/// </para>
/// </remarks>
public sealed class WidgetAdapter : IPayloadAdapter {
    private readonly WidgetContextAccessor _context;

    public WidgetAdapter(WidgetContextAccessor context) => _context = context;

    /// <summary>
    /// A payload carrying <c>widgetContext</c>.
    /// </summary>
    /// <remarks>
    /// Never asked on a widget function, which registers this adapter alone. Written anyway,
    /// because it costs four lines and it is what would let a function serve a widget and an API
    /// Gateway route from one binary. The framework's own <c>ApiGatewayAdapterTests</c> already
    /// uses <c>{"widgetContext":{"widgetId":"w1"}}</c> as a payload the gateway must decline, so
    /// the two are mutually exclusive by construction rather than by luck.
    /// </remarks>
    public bool Handles(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object &&
        payload.TryGetProperty("widgetContext", out var context) &&
        context.ValueKind == JsonValueKind.Object;

    public IExecutionRequest CreateRequest(LambdaPayload payload, ILambdaContext context) {
        var invocation = WidgetInvocation.Read(payload.Raw.Span);

        invocation.Context.InvokedFunctionArn = context.InvokedFunctionArn ?? "";

        // Held for the scope so a handler can take IWidgetContext, and so the Razor helpers can
        // reach the invoked ARN without every template being handed it.
        _context.Current = invocation.Context;

        return WidgetRequest.From(invocation);
    }

    public IExecutionResponse CreateResponse(Stream output) => new LambdaPayloadResponse(output);

    /// <summary>
    /// Answered, not rethrown.
    /// </summary>
    /// <remarks>
    /// A rethrow gives the console a FunctionError and the viewer a blank widget with nothing in it.
    /// Answering lets the error block reach the dashboard, with the request id in it, which is the
    /// only thing that joins what the viewer saw to the function's log. The status itself goes
    /// nowhere: the console reads the body.
    /// </remarks>
    public HostFailurePolicy FailurePolicy => HostFailurePolicy.Answer500;

    /// <summary>
    /// The body, which for this family is the whole answer.
    /// </summary>
    /// <remarks>
    /// Copied across rather than written into. The body a response accumulates and the stream the
    /// runtime sends back are two streams, and an adapter that does nothing here answers empty —
    /// which is a defect <c>InvokeAdapter</c> shipped with and records in its own comment.
    /// </remarks>
    public async ValueTask WriteResponse(IExecutionContext context, Stream output) {
        var body = context.Response.Body;

        if (body.CanSeek) {
            body.Position = 0;
        }

        await body.CopyToAsync(output);
    }
}

/// <summary>
/// Carries the invocation's context from the adapter to whatever resolves <see cref="IWidgetContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// The adapter runs before the scope exists — <c>LambdaInvocationHandler</c> builds the request,
/// then opens the scope — so the context cannot simply be registered as a scoped service by the
/// thing that read it. This is the handoff.
/// </para>
/// <para>
/// <b>A plain field, and that is safe here for a reason worth stating.</b> A Lambda sandbox serves
/// one invocation at a time, so there is no second widget being read while this one is dispatched.
/// An <c>AsyncLocal</c> would suggest otherwise and cost a flow on every await.
/// </para>
/// </remarks>
public sealed class WidgetContextAccessor {
    internal IWidgetContext? Current { get; set; }

    public IWidgetContext Value =>
        Current ?? throw new InvalidOperationException(
            "No widget context. IWidgetContext resolves during an invocation the widget adapter " +
            "built, so a handler outside one - a startup service, a background task - cannot ask " +
            "for it.");
}
