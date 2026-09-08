using Hardened.Requests.Abstract.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace Echo;

/// <summary>
/// The widget's pages.
/// </summary>
/// <remarks>
/// An ordinary class, found by the route table the generator builds from the <c>[Get]</c>
/// attributes. A widget can have as many of these as it has areas; the application class is the
/// one place that composes modules, and it is deliberately somewhere else.
/// </remarks>
public class Pages {
    /// <summary>
    /// Renders the <c>echo</c> parameter, and the event that produced it.
    /// </summary>
    /// <remarks>
    /// The <c>widgetContext</c> below is the point of the sample rather than decoration. A widget
    /// author's first question is what the console actually sends, and the answer is easier to read
    /// on a dashboard than in a log.
    /// </remarks>
    [Get("/")]
    [Output<Views.EchoPage>]
    public EchoPage Index(
        [FromWidget] EchoRequest request,
        IWidgetContext context) =>
        new(request.Echo, context);
}

/// <summary>What the widget was configured or clicked with.</summary>
public class EchoRequest {
    /// <summary>
    /// The HTML to echo back.
    /// </summary>
    /// <remarks>
    /// Written through unescaped, which is what makes this the probe day-one check 3 needs: feeding
    /// markup through here and reading back what survived is how the console's sanitizer gets
    /// documented. A widget that echoed a viewer's input this way would be a different matter, and
    /// the console strips scripts either way.
    /// </remarks>
    public string Echo { get; set; } = "<h1>Hello world</h1>";
}

/// <summary>The model the view renders.</summary>
public record EchoPage(string Echo, IWidgetContext Context);
