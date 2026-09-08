using Hardened.Requests.Abstract.Execution;
using Hardened.Requests.Runtime.QueryString;
using Hardened.Requests.Testing.Conformance;
using Microsoft.Extensions.Primitives;

namespace LambdaWidgets.Runtime.Tests;

/// <summary>
/// <see cref="WidgetRequest"/> held to the same contract as every other transport's request.
///
/// <para>
/// The payload profile rather than the web one, and that is a decision rather than a shortcut. The
/// web profile adds three assertions: a query string is surfaced, its values arrive decoded, and
/// cookies are surfaced. A widget satisfies the first two — the merge is its query string — and
/// cannot satisfy the third, because the console sends no header of any kind and a direct
/// invocation has no cookie channel. Giving this type a cookie list to pass a suite would assert a
/// channel that does not exist.
/// </para>
///
/// <para>
/// The two web assertions that do apply are written out below rather than lost, because the merge
/// is the subtlest thing in the runtime and the framework's own wording for these is better than
/// anything invented here. See FINDINGS.md F-09.
/// </para>
/// </summary>
public class WidgetRequestConformanceTests : PayloadExecutionRequestConformanceTests {
    protected override IExecutionRequestConformanceAdapter Adapter { get; } = new WidgetConformance();

    /// <summary>
    /// The web profile's first extra assertion, which does apply. A widget's query string is the
    /// merge, and it is the only channel a <c>[FromQueryString]</c> parameter has.
    /// </summary>
    [Xunit.Fact]
    public void QueryStringIsSurfaced() {
        var request = Create(spec => {
            spec.QueryString["page"] = "2";
            spec.QueryString["size"] = "50";
        });

        Xunit.Assert.Equal("2", request.QueryString.Get("page").ToString());
        Xunit.Assert.Equal("50", request.QueryString.Get("size").ToString());
    }

    /// <summary>
    /// The second. A widget never percent-encodes, because its values come out of JSON rather than
    /// off a wire — so the characters that told the HTTP transports apart have to arrive untouched
    /// rather than merely decoded.
    /// </summary>
    [Xunit.Fact]
    public void QueryStringValuesArriveIntact() {
        var request = Create(spec => {
            spec.QueryString["asOf"] = "2026-09-10T09:00:00+00:00";
            spec.QueryString["cursor"] = "YWJjZA==";
            spec.QueryString["title"] = "East of Eden";
        });

        Xunit.Assert.Equal("2026-09-10T09:00:00+00:00", request.QueryString.Get("asOf").ToString());
        Xunit.Assert.Equal("YWJjZA==", request.QueryString.Get("cursor").ToString());
        Xunit.Assert.Equal("East of Eden", request.QueryString.Get("title").ToString());
    }

    private sealed class WidgetConformance : IExecutionRequestConformanceAdapter {
        public string TransportName => "CloudWatch widget";

        public IExecutionRequest CreateRequest(ConformanceRequestSpec spec) {
            var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in spec.Headers) {
                headers[header.Key] = header.Value;
            }

            return new WidgetRequest(
                spec.Method,
                spec.Path,
                new SimpleQueryStringCollection(spec.QueryString),
                spec.Body is null ? Stream.Null : new MemoryStream(spec.Body, writable: false),
                headers);
        }
    }
}
