// Roslyn/Infrastructure/AnalyzerOptionExtensions.cs
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure
{
    /// <summary>
    /// Generic cache: for each Compilation, keep one OptionsAccessor per schema type.
    /// Avoids "one ConditionalWeakTable per analyzer".
    /// </summary>
    internal static class OptionAccessorCache
    {
        private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<Type, object>> _byCompilation = new();

        public static OptionsAccessor<TOptions> Get<TOptions, TSchema>(
            Compilation compilation,
            AnalyzerConfigOptionsProvider provider)
            where TSchema : IOptionSchema<TOptions>, new()
        {
            var map = _byCompilation.GetValue(compilation, _ => new ConcurrentDictionary<Type, object>());
            var accessor = (OptionsAccessor<TOptions>)map.GetOrAdd(typeof(TSchema),
                _ => new OptionsAccessor<TOptions>(provider, compilation, new TSchema()));
            return accessor;
        }
    }

    /// <summary>
    /// Extensions analyzers call. Under the hood they use the generic cache.
    /// </summary>
    internal static class AnalyzerOptionExtensions
    {
        public static WeakVarOptions GetWeakVarOptions(this in SyntaxNodeAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<WeakVarOptions, WeakVarOptionsSchema>(
                context.SemanticModel.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForTree(context.Node.SyntaxTree);
        }

        public static WeakVarOptions GetWeakVarOptions(this in SymbolAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<WeakVarOptions, WeakVarOptionsSchema>(
                context.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForSymbol(context);
        }
        public static AsyncVoidOptions GetAsyncVoidOptions(this in SyntaxNodeAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<AsyncVoidOptions, AsyncVoidOptionsSchema>(
                context.SemanticModel.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForTree(context.Node.SyntaxTree);
        }

        public static AsyncVoidOptions GetAsyncVoidOptions(this in OperationAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<AsyncVoidOptions, AsyncVoidOptionsSchema>(
                context.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForTree(context.Operation.Syntax.SyntaxTree);
        }

        public static ConsoleWriteOptions GetConsoleWriteOptions(this in OperationAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<ConsoleWriteOptions, ConsoleWriteOptionsSchema>(
                context.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForTree(context.Operation.Syntax.SyntaxTree);
        }

        public static EmptyCatchOptions GetEmptyCatchOptions(this in SyntaxNodeAnalysisContext context)
        {
            var accessor = OptionAccessorCache.Get<EmptyCatchOptions, EmptyCatchOptionsSchema>(
                context.SemanticModel.Compilation,
                context.Options.AnalyzerConfigOptionsProvider);

            return accessor.ForTree(context.Node.SyntaxTree);
        }
    }

}
