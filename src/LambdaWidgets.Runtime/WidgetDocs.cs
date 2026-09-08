using System.Text.Json;
using Hardened.Requests.Abstract.Execution;
using Hardened.Shared.Runtime.Application;
using Hardened.Requests.Abstract.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace LambdaWidgets.Runtime;

/// <summary>
/// A widget's documentation, which the console's <em>Get documentation</em> button asks for.
/// </summary>
/// <remarks>
/// <para>
/// A widget application registers one. AWS strongly recommends answering describe at all, even with
/// an empty string, so there is a default and nothing has to be written for a widget to behave.
/// </para>
/// <para>
/// <b>Put the widget's parameters in a fenced <c>yaml</c> block.</b> The console lifts the first one
/// out of the markdown and offers to paste it into the widget's parameters editor, which is how a
/// dashboard author discovers what a widget can be configured with. Documentation without one still
/// renders; it just leaves them typing the parameters from prose.
/// </para>
/// <code>
/// [SingletonService(As = typeof(IWidgetDocs))]
/// public class EchoDocs : IWidgetDocs {
///     public string Markdown => """
///         ## Echo
///         ...
///         ```yaml
///         echo: &lt;h1&gt;Hello world&lt;/h1&gt;
///         ```
///         """;
/// }
/// </code>
/// </remarks>
public interface IWidgetDocs {
    string Markdown { get; }
}

/// <summary>
/// What a widget that has not written any documentation answers with.
/// </summary>
/// <remarks>
/// An empty string rather than a refusal, because that is what AWS recommends and because the
/// console's button is there whether or not a widget answers it. A widget that says nothing should
/// show an empty panel rather than an error.
/// </remarks>
internal sealed class NoWidgetDocs : IWidgetDocs {
    public string Markdown => "";
}

/// <summary>
/// Answers <c>describe</c> before anything routes.
/// </summary>
/// <remarks>
/// <para>
/// The console sends the widget's configured parameters on a describe invocation exactly as it does
/// on any other, so there is a route in the event and a handler that would match it. Running it
/// would do the widget's work and throw the answer away — a describe on a search widget would run
/// the search. Short-circuiting is the whole point: this returns without calling
/// <c>chain.Next()</c>, so dispatch never happens.
/// </para>
/// <para>
/// It writes the response itself rather than returning a value, because a filter has no return and
/// because the shape is fixed: the console reads <c>{"markdown": ...}</c> and nothing else.
/// </para>
/// </remarks>
internal sealed class DescribeFilter : IExecutionFilter {
    private readonly WidgetContextAccessor _invocation;
    private readonly IWidgetDocs _docs;

    public DescribeFilter(WidgetContextAccessor invocation, IWidgetDocs docs) {
        _invocation = invocation;
        _docs = docs;
    }

    public async Task Execute(IExecutionChain chain) {
        if (!_invocation.Describe) {
            await chain.Next();

            return;
        }

        var response = chain.Context.Response;

        // application/json, so the adapter copies this through rather than quoting it as a
        // rendered view. The answer is an object; a widget's HTML answer is a string.
        response.ContentType = "application/json";

        await using var writer = new Utf8JsonWriter(response.Body);

        writer.WriteStartObject();
        writer.WriteString("markdown", _docs.Markdown);
        writer.WriteEndObject();

        await writer.FlushAsync();
    }
}

/// <summary>Puts <see cref="DescribeFilter"/> in the chain, ahead of dispatch.</summary>
/// <remarks>
/// A startup service rather than a registration, because the chain is assembled at start and
/// <c>LambdaInvocationHandler</c> appends dispatch to the end of it on the first invocation. A
/// filter added here therefore runs before dispatch without having to say so.
/// </remarks>
internal sealed class DescribeStartupService : IStartupService {
    public Task<bool> Startup(IServiceProvider rootProvider) {
        var filter = rootProvider.GetRequiredService<DescribeFilter>();

        rootProvider.GetRequiredService<IMiddlewareService>().Use(_ => filter);

        return Task.FromResult(true);
    }
}
