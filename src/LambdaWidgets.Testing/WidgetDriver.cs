using System.Text;
using Amazon.Lambda.Core;
using Hardened.Aws.Lambda.Runtime.Hosting;
using LambdaWidgets.Dashboard;

namespace LambdaWidgets.Testing;

/// <summary>
/// A widget under test, driven the way a viewer drives one.
/// </summary>
/// <remarks>
/// <para>
/// A test opens the widget, fills its fields, clicks something by the text a viewer would read, and
/// asserts on what came back. It never names a route, a payload or a JSON field, because a viewer
/// cannot — and a test that did would break when the widget was rewritten without its behaviour
/// changing.
/// </para>
/// <para>
/// In process, with no test tool and no harness running. The invocation goes through the real
/// <c>LambdaInvocationHandler</c>, so the adapter, the merge, dispatch and the response path are all
/// exercised; what comes back is read by <see cref="IWidgetConsole"/>, the same interpreter the
/// harness renders with. That is what makes a passing driver test mean the harness will behave the
/// same way.
/// </para>
/// </remarks>
public interface IWidgetDriver {
    /// <summary>
    /// The dashboard the widget is on: time range, theme, period, account.
    /// </summary>
    /// <remarks>Set it before opening, the way a viewer sets the dashboard's controls.</remarks>
    DashboardState State { get; set; }

    /// <summary>The widget's configured parameters, as a dashboard author set them.</summary>
    IDictionary<string, string> Params { get; }

    /// <summary>What is on screen. Empty until <see cref="Open"/>.</summary>
    ShownWidget Shown { get; }

    /// <summary>What is on screen, as HTML.</summary>
    string Html { get; }

    /// <summary>Invokes the widget the way the console does when it appears on a dashboard.</summary>
    Task<ShownWidget> Open();

    /// <summary>
    /// Re-invokes with the configured parameters, the way a refresh or a time range change does.
    /// </summary>
    /// <remarks>
    /// The assertion worth writing after this is that the widget is back on its landing page. A
    /// widget that carries its position through a refresh is a widget that will surprise its author
    /// on a real dashboard.
    /// </remarks>
    Task<ShownWidget> Refresh();

    /// <summary>Asks for documentation, the way the console's button does.</summary>
    Task<WidgetResponse> Describe();

    /// <summary>Types into one of the widget's fields.</summary>
    /// <exception cref="InvalidOperationException">The widget has no field with that name.</exception>
    void Fill(string field, string value);

    /// <summary>
    /// Clicks the element with this text, and answers the invocation it caused.
    /// </summary>
    /// <remarks>
    /// By the text a viewer reads rather than by index, because that is what a viewer clicks and
    /// what a failure should name. An index overload exists for a widget whose buttons share text.
    /// A confirmation is accepted; <see cref="Decline"/> is the other answer.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Nothing on screen has that text.</exception>
    Task<ShownWidget> Click(string text);

    /// <summary>Clicks the action at this index among those on screen.</summary>
    Task<ShownWidget> Click(int index);

    /// <summary>
    /// Answers no to the element's confirmation, so nothing is invoked and the screen does not
    /// change.
    /// </summary>
    /// <remarks>
    /// The assertion this is for is that declining a destructive action really does nothing — the
    /// property a confirmation exists to provide, and one nobody checks until it has failed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">That element carries no confirmation to decline.</exception>
    ShownWidget Decline(string text);
}

/// <inheritdoc />
internal sealed class WidgetDriver : IWidgetDriver {
    private static readonly ShownWidget Nothing = new(
        ResponseKind.Html, "", "", Array.Empty<WidgetAction>(),
        new Dictionary<string, string>(), Array.Empty<Removal>(), Array.Empty<Finding>());

    private readonly LambdaInvocationHandler _handler;
    private readonly IWidgetConsole _console;
    private readonly Dictionary<string, string> _edits = new(StringComparer.Ordinal);

    public WidgetDriver(LambdaInvocationHandler handler, IWidgetConsole console) {
        _handler = handler;
        _console = console;
    }

    public DashboardState State { get; set; } = new();

    public IDictionary<string, string> Params { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public ShownWidget Shown { get; private set; } = Nothing;

    public string Html => Shown.Html;

    public async Task<ShownWidget> Open() {
        _edits.Clear();

        return await Send(_console.Opens(Widget(), State));
    }

    /// <remarks>
    /// The edits are dropped, because the console keeps no state between invocations and a refresh
    /// re-renders whatever the function returns. A driver that kept them would let a test pass on
    /// values the console would have thrown away.
    /// </remarks>
    public async Task<ShownWidget> Refresh() {
        _edits.Clear();

        return await Send(_console.Refreshes(Widget(), State));
    }

    /// <remarks>
    /// Returns the response rather than a shown widget, because documentation is markdown and has
    /// no actions or fields to interrogate.
    /// </remarks>
    public async Task<WidgetResponse> Describe() =>
        new WidgetResponses().Classify(await Invoke(_console.AsksForDocumentation(Widget(), State)));

    public void Fill(string field, string value) {
        if (!Shown.Forms.ContainsKey(field)) {
            throw new InvalidOperationException(
                $"This widget has no field named '{field}'. It has: " +
                (Shown.Forms.Count == 0 ? "none" : string.Join(", ", Shown.Forms.Keys)) +
                ". A field the console can see needs a name attribute.");
        }

        _edits[field] = value;
    }

    public Task<ShownWidget> Click(string text) => Click(Find(text).Index);

    public async Task<ShownWidget> Click(int index) {
        var next = _console.Clicks(Shown, index, _edits, Widget(), State);

        _edits.Clear();

        return await Send(next);
    }

    public ShownWidget Decline(string text) {
        var action = Find(text);

        if (action.Confirmation is null) {
            throw new InvalidOperationException(
                $"'{text}' carries no confirmation, so there is nothing to decline. Add one with " +
                "Widget.Confirm if this action should ask first.");
        }

        // Nothing is invoked and nothing changes, which is the whole of what declining does.
        return Shown;
    }

    /// <remarks>
    /// Matched on the text a viewer reads, trimmed. A failure names what is on screen, because the
    /// likely cause is that the widget rendered a different page than the test expected and a
    /// message saying only "not found" sends the reader to the wrong place.
    /// </remarks>
    private WidgetAction Find(string text) =>
        Shown.Actions.FirstOrDefault(action =>
            string.Equals(action.Text, text.Trim(), StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Nothing on screen reads '{text}'. This widget offers: " +
            (Shown.Actions.Count == 0
                ? "nothing - has it been opened?"
                : string.Join(", ", Shown.Actions.Select(action => $"'{action.Text}'"))) + ".");

    private async Task<ShownWidget> Send(WidgetEvent widgetEvent) =>
        Shown = _console.Shows(await Invoke(widgetEvent), State);

    private async Task<string> Invoke(WidgetEvent widgetEvent) {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(widgetEvent.ToJson()));

        var output = await _handler.Invoke(input, new DriverContext());

        return Encoding.UTF8.GetString(((MemoryStream)output).ToArray());
    }

    /// <remarks>
    /// A synthetic widget, because a driver test is about one widget's behaviour rather than about
    /// a dashboard file. The endpoint is the ARN the helpers write into every <c>cwdb-action</c>,
    /// so a rendered link points back at the widget under test.
    /// </remarks>
    private DashboardWidget Widget() => new() {
        Id = "widget-1",
        Endpoint = DriverContext.Arn,
        Params = new Dictionary<string, string>(Params, StringComparer.Ordinal)
    };

    /// <summary>Enough of an invocation for the adapter to build a request from.</summary>
    private sealed class DriverContext : ILambdaContext {
        public const string Arn = "arn:aws:lambda:us-east-1:000000000000:function:widget-under-test";

        public string AwsRequestId { get; } = Guid.NewGuid().ToString();
        public IClientContext ClientContext => null!;
        public string FunctionName => "widget-under-test";
        public string FunctionVersion => "$LATEST";
        public ICognitoIdentity Identity => null!;
        public string InvokedFunctionArn => Arn;
        public ILambdaLogger Logger => null!;
        public string LogGroupName => "/aws/lambda/widget-under-test";
        public string LogStreamName => "driver";
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
    }
}
