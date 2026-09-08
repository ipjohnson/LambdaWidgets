using DependencyModules.Runtime.Attributes;
using Hardened.Shared.Runtime.Attributes;
using Hardened.Web.Runtime.Attributes;
using LambdaWidgets.Runtime;

namespace Echo;

/// <summary>
/// AWS's Echo widget, in C#.
/// </summary>
/// <remarks>
/// The first sample deliberately, because it is the one AWS documents and the one a reader can
/// compare line for line with the Python and JavaScript versions on the custom widget samples page.
/// It does the least a widget can do: render what it was given, and say what it takes.
/// </remarks>
[HardenedModule]
[LambdaWidgetModule]
[Enable<WidgetTemplates>]
public partial class EchoApp { }
