using Hardened.Requests.Abstract.Execution;
using Hardened.Requests.Abstract.PathTokens;
using Hardened.Requests.Abstract.QueryString;
using Hardened.Requests.Runtime.PathTokens;
using Hardened.Requests.Runtime.QueryString;
using Microsoft.Extensions.Primitives;

namespace LambdaWidgets.Runtime;

/// <summary>
/// A widget invocation as the pipeline sees it: web-shaped, delivered as a direct invoke.
/// </summary>
/// <remarks>
/// <para>
/// The framework's own words, on <c>IPayloadAdapter</c>: "a CloudWatch widget is web-shaped despite
/// arriving as a direct invoke". A handler sees a path and a query string and answers with a
/// document, so the route table matches <c>GET /search</c> the way it matches any other route and
/// the ordinary binding fills the handler's request object.
/// </para>
/// <para>
/// <b>The merge is both the query string and the body.</b> There is no query string on the wire and
/// the body is not the console's event. Both carry <c>widgetContext.params</c>, the event's
/// top-level fields and <c>widgetContext.forms.all</c> merged in that order, because Hardened binds
/// the two differently and a widget handler needs each.
/// </para>
/// <para>
/// <b>No cookies, and it enrols in the payload conformance profile for that reason.</b> The web
/// profile adds three assertions and the third needs a cookie the transport carried; the console
/// sends no header of any kind, so a widget has nowhere one could come from. Giving this type a
/// cookie list to satisfy a suite would be asserting a channel that does not exist. See
/// FINDINGS.md F-09.
/// </para>
/// </remarks>
public sealed class WidgetRequest : IExecutionRequest {
    /// <summary>
    /// The scheme a widget route is matched under.
    /// </summary>
    /// <remarks>
    /// GET, rather than a scheme of its own like <c>QUEUE</c> or <c>INVOKE</c>. A widget's pages
    /// are ordinary <c>[Get]</c> routes so that the generated <c>Links</c> and <c>Routes</c> types
    /// cover them, which is what lets a template name a handler instead of a string. The console
    /// sends no method, and a <c>[Post]</c> handler in a widget would be unreachable.
    /// </remarks>
    public const string Scheme = "GET";

    private IPathTokenCollection? _pathTokens;

    internal WidgetRequest(
        string method,
        string path,
        IQueryStringCollection queryString,
        Stream body,
        IDictionary<string, StringValues> headers) {
        Method = method;
        Path = path;
        QueryString = queryString;
        Body = body;
        Headers = headers;
    }

    /// <summary>
    /// Builds one from an invocation's merge, which becomes the query string and the body at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both, because Hardened binds the two differently and a widget needs each.</b> A parameter
    /// carrying <c>[FromQueryString]</c> is converted from a string; a complex parameter with no
    /// attribute is "inferred for a complex type with no other source" and deserialized from the
    /// body. Writing the merge only to the query string leaves a handler's request object empty,
    /// which is what the first version of this did.
    /// </para>
    /// <para>
    /// The console's own event is not the body. Nothing binds it, a handler wanting the dashboard
    /// takes <see cref="IWidgetContext"/>, and leaving the raw event there is what made an empty
    /// request object look like successful binding: a payload whose top-level fields happened to
    /// match the handler's type deserialized straight out of it.
    /// </para>
    /// </remarks>
    internal static WidgetRequest From(WidgetInvocation invocation) =>
        new(Scheme,
            invocation.Route,
            new SimpleQueryStringCollection(
                invocation.Query.ToDictionary(
                    value => value.Key,
                    value => value.Value.Text,
                    StringComparer.OrdinalIgnoreCase)),
            MergedBody(invocation.Query),
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase));

    /// <summary>The merge as a JSON object, for the deserializer a complex parameter goes through.</summary>
    private static Stream MergedBody(IReadOnlyDictionary<string, WidgetValue> query) {
        var buffer = new MemoryStream();

        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer)) {
            writer.WriteStartObject();

            foreach (var value in query) {
                value.Value.Write(writer, value.Key);
            }

            writer.WriteEndObject();
        }

        buffer.Position = 0;

        return buffer;
    }

    public string Method { get; }

    public string Path { get; }

    /// <summary>
    /// JSON, because the body is a JSON object this type wrote and the deserializer reads this to
    /// decide it can handle it.
    /// </summary>
    /// <remarks>
    /// A header wins where one was set. The console sends none, so in production this is always the
    /// fallback — but a request whose header says otherwise and whose <c>ContentType</c> disagrees
    /// is a request that lies, and the conformance suite is right to insist.
    /// </remarks>
    public string? ContentType => Header("Content-Type") ?? "application/json";

    /// <summary>
    /// HTML. A widget answers with a document, and content negotiation has no other signal to read.
    /// </summary>
    /// <remarks>A header wins where one was set, for the reason above.</remarks>
    public string? Accept => Header("Accept") ?? "text/html";

    private string? Header(string name) =>
        Headers.TryGetValue(name, out var value) && value.Count > 0 ? value.ToString() : null;

    public IExecutionRequestParameters? Parameters { get; set; }

    public Stream Body { get; set; }

    /// <summary>
    /// Empty, and filled by nothing.
    /// </summary>
    /// <remarks>
    /// A widget invocation carries no headers. The console sends the event and nothing around it,
    /// and a direct invoke's only header-like channel is the SDK caller's client context, which the
    /// console does not set. Mutable because the pipeline expects to be able to add its own.
    /// </remarks>
    public IDictionary<string, StringValues> Headers { get; }

    public IQueryStringCollection QueryString { get; }

    /// <summary>
    /// The route's path parameters, percent-decoded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The decode is missing everywhere else, and this is the only place it can happen.</b> The
    /// generated <c>Routes</c> escape a path argument with <c>Uri.EscapeDataString</c>, so
    /// <c>Routes.Detail("SHIPMENT#SHP1000")</c> is <c>/shipment/SHIPMENT%23SHP1000</c>. Over HTTP
    /// the server undoes that before the route table reads the path; a widget invocation carries
    /// its route as a string inside the action's JSON and has no server in that position, so
    /// without this a handler binds <c>SHIPMENT%23SHP1000</c> and the lookup quietly finds nothing.
    /// Every key in a single-table DynamoDB design carries a <c>#</c>, which is the first thing
    /// that happens to anyone routing on one.
    /// </para>
    /// <para>
    /// <b>Per token rather than over the whole path, and that is the difference that matters.</b>
    /// Decoding before the match would turn a <c>%2F</c> into a segment separator and a key holding
    /// a slash would stop matching its own route. The route table matches the escaped path, so an
    /// escaped separator stays inside one segment, and only the captured value is decoded — which
    /// makes this the exact inverse of the escape the generated link applied.
    /// </para>
    /// </remarks>
    public IPathTokenCollection PathTokens {
        get => _pathTokens ?? PathTokenCollection.Empty;
        set => _pathTokens = Decoded(value);
    }

    /// <remarks>
    /// A value with no <c>%</c> in it is returned as it came, so the common route pays nothing and
    /// a token that was never escaped cannot be changed by this.
    /// </remarks>
    private static IPathTokenCollection Decoded(IPathTokenCollection tokens) {
        if (tokens.Count == 0) {
            return tokens;
        }

        var decoded = new PathTokenCollection(tokens.Count);

        for (var index = 0; index < tokens.Count; index++) {
            var token = tokens.Get(index);

            decoded.Set(index, token.TokenValue.Contains('%', StringComparison.Ordinal)
                ? token with { TokenValue = Uri.UnescapeDataString(token.TokenValue) }
                : token);
        }

        return decoded;
    }

    /// <summary>Nothing. There is no header to parse cookies from and no browser to have set one.</summary>
    public IReadOnlyList<string> Cookies => Array.Empty<string>();

    /// <summary>
    /// Nothing, because an invocation through the Lambda API has no connection to describe.
    /// </summary>
    /// <remarks>
    /// The viewer is on the far side of the console, which invoked this function with its own
    /// credentials. Their address is not in the event, and putting the console's there would fill a
    /// field callers read as the client's.
    /// </remarks>
    public ITransportInfo Transport => EmptyTransportInfo.Instance;

    /// <remarks>
    /// Null means keep the current value, matching the rest of the <c>Clone</c> contract.
    /// <paramref name="cookies"/> is accepted and ignored: this shape has nowhere to put one, and
    /// refusing would break a filter that clones with the arguments it was given rather than the
    /// ones this transport can use.
    /// </remarks>
    public IExecutionRequest Clone(
        string? method = null,
        string? path = null,
        IDictionary<string, StringValues>? headers = null,
        IQueryStringCollection? queryString = null,
        IReadOnlyList<string>? cookies = null) =>
        new WidgetRequest(
            method ?? Method,
            path ?? Path,
            queryString ?? QueryString,
            Body,
            headers ?? Headers) {
            // Cloned rather than shared: a forked chain rebinds its own parameters, and sharing
            // them would let one fork overwrite another's. Null stays null, because a request that
            // has not been bound yet has nothing to copy.
            Parameters = Parameters?.Clone(),
            // The field rather than the property, because these are decoded already and the setter
            // decodes. A token holding a literal percent sign would lose it on every clone.
            _pathTokens = _pathTokens
        };
}
