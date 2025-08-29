using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure.Common;

internal static class AnalyzerOptions
{
    /// <summary>
    /// Reads typed analyzer options for the given <see cref="SyntaxNodeAnalysisContext"/>.
    /// Binds once per tree using a per-compilation cache.
    /// </summary>
    /// <typeparam name="TOptions">Typed options model.</typeparam>
    /// <typeparam name="TSchema">Schema that knows defaults and how to bind .editorconfig.</typeparam>
    // Syntax node context
    public static TOptions GetOptions<TOptions, TSchema>(this in SyntaxNodeAnalysisContext context)
        where TSchema : IOptionSchema<TOptions>, new()
    {
        var accessor = OptionAccessorCache.Get<TOptions, TSchema>(
            context.SemanticModel.Compilation,
            context.Options.AnalyzerConfigOptionsProvider);

        return accessor.ForTree(context.Node.SyntaxTree);
    }

    /// <summary>
    /// Reads typed analyzer options for the given <see cref="SymbolAnalysisContext"/>.
    /// Prefers the first in-source location of the symbol to pick the right tree.
    /// </summary>
    public static TOptions GetOptions<TOptions, TSchema>(this in SymbolAnalysisContext context)
        where TSchema : IOptionSchema<TOptions>, new()
    {
        var accessor = OptionAccessorCache.Get<TOptions, TSchema>(
            context.Compilation,
            context.Options.AnalyzerConfigOptionsProvider);

        return accessor.ForSymbol(context);
    }

    /// <summary>
    /// Reads typed analyzer options for the given <see cref="OperationAnalysisContext"/>.
    /// Uses the operation's syntax tree to obtain the correct per-tree snapshot.
    /// </summary>
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
