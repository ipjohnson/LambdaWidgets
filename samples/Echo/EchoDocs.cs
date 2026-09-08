using DependencyModules.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace Echo;

/// <summary>
/// What the console's <em>Get documentation</em> button shows.
/// </summary>
/// <remarks>
/// AWS's own Echo documentation, near enough verbatim. The fenced <c>yaml</c> block is what the
/// console lifts into the widget's parameters editor, which is how a dashboard author finds out
/// what a widget takes without reading its source.
/// </remarks>
[SingletonService(As = typeof(IWidgetDocs))]
public class EchoDocs : IWidgetDocs {
    public string Markdown =>
        """
        ## Echo

        A basic echo script. Anything passed in the **echo** parameter is returned as the content
        of the widget, and the `widgetContext` the console sent is shown below it.

        ### Widget parameters

        Parameter | Description
        ---|---
        **echo** | The HTML to echo back

        ### Example parameters

        ```yaml
        echo: <h1>Hello world</h1>
        ```
        """;
}
