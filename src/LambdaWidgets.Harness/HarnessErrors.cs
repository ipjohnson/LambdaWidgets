using Hardened.Requests.Abstract.Errors;
using Hardened.Requests.Abstract.Execution;
using Hardened.Requests.Runtime.Errors;

namespace LambdaWidgets.Harness;

/// <summary>
/// Lets the harness's own messages reach the caller.
/// </summary>
/// <remarks>
/// <para>
/// <b>The messages are most of what a local tool is for, and the pipeline was dropping them.</b>
/// Asking for a widget that is not there produced exactly the answer a dev tool should give —
/// <em>This dashboard has no widget '0'. It has: widget-1, widget-2, widget-3.</em> — and the HTTP
/// response was <c>{"type":"ServerError","message":"The server could not complete this
/// request.","details":""}</c>, with the good message only in the server's log. An unrecognised
/// exception is a 500 whose message the caller must not see; these three are the harness telling
/// its caller what they got wrong.
/// </para>
/// <para>
/// Registered in <c>Program.cs</c> ahead of the module, because the framework registers its own
/// with <c>TryAdd</c> and first wins.
/// </para>
/// </remarks>
public sealed class HarnessErrors : IExceptionToModelConverter {
    private readonly ExceptionToModelConverter _rest = new();

    public (int, object) ConvertExceptionToModel(IExecutionContext context, Exception exception) =>
        exception switch {
            // No dashboard with that id: HarnessRegistry, which names how to make one.
            KeyNotFoundException => (404, Model("NotFound", exception)),

            // No widget with that id, or an action index the shown widget does not have.
            ArgumentOutOfRangeException => (404, Model("NotFound", exception)),

            // A widget that has not been rendered, so there is nothing to click.
            InvalidOperationException => (409, Model("Conflict", exception)),

            _ => _rest.ConvertExceptionToModel(context, exception)
        };

    private static ErrorModel Model(string type, Exception exception) =>
        new() { Type = type, Message = exception.Message };
}
