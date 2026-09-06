using Microsoft.CodeAnalysis;

namespace LambdaWidgets.SourceGenerator;

/// <summary>
/// Emits the entry point for a <c>[LambdaWidgetModule]</c> application: a <c>Main</c> that asks the
/// Amz emulator for a session and builds the RuntimeSupport bootstrap, and the
/// <c>Invoke(WidgetEvent, ILambdaContext)</c> that <c>LambdaWidgets.Testing</c> drives in-process.
///
/// <para>
/// Registered and emitting nothing until item 2. It exists now so the build compiles the
/// source-shipped Hardened.SourceGenerator, CSharpAuthor and ValidationModules sources that this
/// assembly is made of, which is the part of the arrangement that fails loudly and late.
/// </para>
/// </summary>
[Generator]
public class WidgetApplicationGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
    }
}
