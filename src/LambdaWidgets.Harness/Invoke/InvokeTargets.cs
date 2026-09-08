namespace LambdaWidgets.Harness.Invoke;

/// <summary>Where the harness sends an invoke for a given function name.</summary>
/// <param name="Address">The service's base address.</param>
/// <param name="Path">The Invoke API path, with <c>{name}</c> where the function name goes.</param>
/// <param name="Description">What to print at start, so a reader knows where clicks are going.</param>
public sealed record InvokeTarget(string Address, string Path, string Description) {
    /// <summary>The Invoke API's own route, which every emulator implements.</summary>
    public const string InvokePath = "/2015-03-31/functions/{name}/invocations";

    /// <summary>The AWS Lambda Test Tool, which is what <c>dotnet run</c> on a sample starts.</summary>
    public static InvokeTarget TestTool(int port = 5050) =>
        new($"http://localhost:{port}", InvokePath, $"the AWS Lambda Test Tool on {port}");

    /// <summary>
    /// The Runtime Interface Emulator, which serves one function per container.
    /// </summary>
    /// <remarks>
    /// The name in the path is literally <c>function</c> rather than the widget's, because a
    /// container holds one function and the RIE does not route by name. A widget whose endpoint
    /// names something else still reaches it, which is the behaviour a single-container run wants.
    /// </remarks>
    public static InvokeTarget Rie(string address) =>
        new(address, "/2015-03-31/functions/function/invocations", $"a runtime interface emulator at {address}");

    /// <summary><c>sam local start-lambda</c>.</summary>
    public static InvokeTarget Sam(string address) =>
        new(address, InvokePath, $"SAM local at {address}");

    /// <summary>An address given by name on the command line.</summary>
    public static InvokeTarget Named(string address) =>
        new(address, InvokePath, address);

    /// <summary>The URL an invoke for <paramref name="functionName"/> goes to.</summary>
    public Uri UriFor(string functionName) =>
        new(Address.TrimEnd('/') + Path.Replace("{name}", Uri.EscapeDataString(functionName)));
}

/// <summary>
/// Which target serves which function name.
/// </summary>
/// <remarks>
/// <para>
/// A widget's <c>cwdb-action</c> carries an ARN, and the segment after <c>function:</c> is what
/// selects a target. That is what lets one dashboard hold widgets served by different things at
/// once — one running under the test tool, one in a container, one deployed.
/// </para>
/// <para>
/// A name given explicitly wins over the default, whatever order the flags were written in.
/// </para>
/// </remarks>
public sealed class InvokeRouting {
    private readonly Dictionary<string, InvokeTarget> _byName;

    public InvokeRouting(InvokeTarget fallback, IReadOnlyDictionary<string, InvokeTarget>? byName = null) {
        Fallback = fallback;
        _byName = byName is null
            ? new Dictionary<string, InvokeTarget>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, InvokeTarget>(byName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Where a name with no explicit target goes.</summary>
    public InvokeTarget Fallback { get; }

    public InvokeTarget For(string functionName) =>
        _byName.TryGetValue(functionName, out var target) ? target : Fallback;

    /// <summary>
    /// Reads the target flags out of a command line.
    /// </summary>
    /// <remarks>
    /// Unrecognised arguments are ignored rather than refused: the harness takes other flags, and a
    /// parser that owns the whole command line has to know about all of them.
    /// </remarks>
    public static InvokeRouting From(IReadOnlyList<string> arguments) {
        InvokeTarget fallback = InvokeTarget.TestTool();

        var byName = new Dictionary<string, InvokeTarget>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < arguments.Count; i++) {
            var next = i + 1 < arguments.Count ? arguments[i + 1] : null;

            switch (arguments[i]) {
                case "--test-tool":
                    fallback = InvokeTarget.TestTool(
                        int.TryParse(next, out var port) ? port : 5050);
                    break;

                case "--rie":
                    fallback = InvokeTarget.Rie(next ?? "http://localhost:9000");
                    break;

                case "--sam":
                    fallback = InvokeTarget.Sam(next ?? "http://127.0.0.1:3001");
                    break;

                case "--function" when next is not null:
                    var split = next.IndexOf('=');

                    if (split > 0) {
                        byName[next[..split]] = InvokeTarget.Named(next[(split + 1)..]);
                    }

                    break;
            }
        }

        return new InvokeRouting(fallback, byName);
    }
}
