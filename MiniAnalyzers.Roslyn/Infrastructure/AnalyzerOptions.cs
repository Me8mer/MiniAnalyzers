using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure;

internal static class AnalyzerOptions
{
    // Syntax node context
    public static TOptions GetOptions<TOptions, TSchema>(this in SyntaxNodeAnalysisContext context)
        where TSchema : IOptionSchema<TOptions>, new()
    {
        var accessor = OptionAccessorCache.Get<TOptions, TSchema>(
            context.SemanticModel.Compilation,
            context.Options.AnalyzerConfigOptionsProvider);

        return accessor.ForTree(context.Node.SyntaxTree);
    }

    // Symbol context
    public static TOptions GetOptions<TOptions, TSchema>(this in SymbolAnalysisContext context)
        where TSchema : IOptionSchema<TOptions>, new()
    {
        var accessor = OptionAccessorCache.Get<TOptions, TSchema>(
            context.Compilation,
            context.Options.AnalyzerConfigOptionsProvider);

        return accessor.ForSymbol(context);
    }

    // Operation context
    public static TOptions GetOptions<TOptions, TSchema>(this in OperationAnalysisContext context)
        where TSchema : IOptionSchema<TOptions>, new()
    {
        var accessor = OptionAccessorCache.Get<TOptions, TSchema>(
            context.Compilation,
            context.Options.AnalyzerConfigOptionsProvider);

        // Operations carry syntax. Binding by tree is accurate and cheap.
        return accessor.ForTree(context.Operation.Syntax.SyntaxTree);
    }
}
