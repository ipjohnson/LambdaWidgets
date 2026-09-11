using System.Net.Sockets;
using System.Reflection;

namespace LambdaWidgets.Harness;

/// <summary>
/// What the command line asked for, read before anything is built.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here rather than in <c>Program.cs</c> so it can be tested.</b> <c>--help</c> used to fall
/// through to <see cref="Invoke.InvokeRouting.From"/>, which ignores what it does not recognise, so
/// it bound the port, served an empty dashboard, printed nothing and did not exit — and the next
/// real run died on a Kestrel stack trace rather than saying the port was in use. Neither is the
/// kind of thing a test reaches through a top-level statement.
/// </para>
/// <para>
/// Unrecognised arguments are still ignored. The host reads its own, and a parser that refused what
/// it did not know would have to know about all of them.
/// </para>
/// </remarks>
public static class CommandLine {
    /// <summary>Whether to print <see cref="Usage"/> and exit.</summary>
    public static bool WantsHelp(IReadOnlyList<string> arguments) =>
        arguments.Any(argument => argument is "--help" or "-h" or "-?" or "/?");

    /// <summary>Whether to print <see cref="Version"/> and exit.</summary>
    public static bool WantsVersion(IReadOnlyList<string> arguments) =>
        arguments.Contains("--version");

    /// <summary>The file named by <c>--dashboard</c>, or null when the flag was not given.</summary>
    /// <remarks>
    /// The distinction is the whole of it. A file named explicitly and not there is a typo, and
    /// saying so beats an empty dashboard that looks exactly like a dashboard with no widgets in
    /// it. No flag at all in a directory with no file still starts, which is what lets
    /// <c>lambda-widgets</c> be tried on its own.
    /// </remarks>
    public static string? Dashboard(IReadOnlyList<string> arguments) {
        for (var i = 0; i + 1 < arguments.Count; i++) {
            if (arguments[i] == "--dashboard") {
                return arguments[i + 1];
            }
        }

        return null;
    }

    /// <summary>Whether the host failed to start because something already holds the port.</summary>
    /// <remarks>
    /// Matched on the socket error rather than on Kestrel's <c>AddressInUseException</c>, which
    /// lives in a package this project does not reference and reaches it only transitively.
    /// </remarks>
    public static bool IsPortInUse(Exception? failure) =>
        failure is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } ||
        (failure?.InnerException is not null && IsPortInUse(failure.InnerException));

    /// <remarks>
    /// The informational version carries the commit after a <c>+</c>, which is for a build log
    /// rather than for whoever asked what they are running.
    /// </remarks>
    public static string Version {
        get {
            var informational = typeof(CommandLine).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (string.IsNullOrEmpty(informational)) {
                return typeof(CommandLine).Assembly.GetName().Version?.ToString() ?? "unknown";
            }

            var build = informational.IndexOf('+');

            return build < 0 ? informational : informational[..build];
        }
    }

    public static string Usage => """
        lambda-widgets - the CloudWatch console's side of a custom widget, on your machine.

        Usage:
          lambda-widgets [--dashboard <file>] [<target>] [--function <name>=<url>]...

        Options:
          --dashboard <file>       The dashboard body to render. Default dashboard.json
          --test-tool [<port>]     Invoke through the AWS Lambda Test Tool. The default, on 5050
          --rie [<address>]        Invoke through a runtime interface emulator. Default
                                   http://localhost:9000
          --sam [<address>]        Invoke through `sam local start-lambda`. Default
                                   http://127.0.0.1:3001
          --function <name>=<url>  Send one function's invokes elsewhere. Repeatable, and it wins
                                   over the target above whatever order the flags are written in
          --version                Print the version and exit
          --help                   Print this and exit

        Environment:
          PORT                     The port to listen on. Default 5080
        """;
}
