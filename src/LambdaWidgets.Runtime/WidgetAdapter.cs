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
    private readonly IWidgetErrors _errors;

    public WidgetAdapter(WidgetContextAccessor context, IWidgetErrors errors) {
        _context = context;
        _errors = errors;
    }

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
        _context.Describe = invocation.Describe;
        _context.RequestId = context.AwsRequestId ?? "";

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
    /// The body, as JSON, because that is what the caller of an Invoke reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A rendered view has to be quoted on the way out, and this is the only place that can
    /// happen.</b> The console reads the Invoke response as JSON and renders the string it finds. A
    /// handler returning a <c>string</c> or an object is serialized by the IO filter and arrives
    /// here as JSON already; a handler returning a view is not — the template writes raw markup
    /// straight into the body, and copying that through answers with HTML where JSON was expected.
    /// Every template-based widget would fail in the console and work in every test that read the
    /// body directly.
    /// </para>
    /// <para>
    /// The content type is what tells the two apart, because it is the one thing the pipeline sets
    /// differently: a view declares <c>text/html</c> through its <c>[TemplateContentType]</c> and a
    /// serialized response declares <c>application/json</c>.
    /// </para>
    /// <para>
    /// Copied rather than written into, in both branches. The body a response accumulates and the
    /// stream the runtime sends back are two streams, and an adapter that does nothing here answers
    /// empty — a defect <c>InvokeAdapter</c> shipped with and records in its own comment.
    /// </para>
    /// </remarks>
    public async ValueTask WriteResponse(IExecutionContext context, Stream output) {
        // The serialized ErrorModel is already in the body by now, and it is the wrong answer: the
        // console renders an object it does not recognise as JSON, so a widget that threw shows an
        // operator a JSON blob where a designed page should be. The page is written instead of it
        // rather than beside it.
        if (context.Response.ExceptionValue is { } failure) {
            await Quoted(output, _errors.Page(failure, _context.RequestId));

            return;
        }

        var body = context.Response.Body;

        if (body.CanSeek) {
            body.Position = 0;
        }

        if (!IsHtml(context.Response.ContentType)) {
            await body.CopyToAsync(output);

            return;
        }

        var rendered = new MemoryStream();

        await body.CopyToAsync(rendered);

        await using var writer = new Utf8JsonWriter(output);

        // The markup is already UTF-8 and the writer wants UTF-8, so this escapes in place rather
        // than round-tripping the page through a UTF-16 string.
        writer.WriteStringValue(
            rendered.TryGetBuffer(out var buffer) ? buffer.AsSpan() : rendered.ToArray());

        await writer.FlushAsync();
    }

    private static async ValueTask Quoted(Stream output, string html) {
        await using var writer = new Utf8JsonWriter(output);

        writer.WriteStringValue(html);

        await writer.FlushAsync();
    }

    private static bool IsHtml(string? contentType) =>
        contentType is not null &&
        contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Whether this invocation is the console asking for documentation.</summary>
    /// <remarks>
    /// Here rather than on <see cref="IWidgetContext"/>, which is what the dashboard sent. Describe
    /// is a property of the invocation, and a handler never sees one: the filter answers before
    /// anything routes.
    /// </remarks>
    internal bool Describe { get; set; }

    /// <summary>
    /// This invocation's request id, which is the only thing joining what a viewer saw to the
    /// function's log.
    /// </summary>
    internal string RequestId { get; set; } = "";

    public IWidgetContext Value =>
        Current ?? throw new InvalidOperationException(
            "No widget context. IWidgetContext resolves during an invocation the widget adapter " +
            "built, so a handler outside one - a startup service, a background task - cannot ask " +
            "for it.");
}
