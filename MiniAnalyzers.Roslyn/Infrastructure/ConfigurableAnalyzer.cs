using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure
{
    /// <summary>
    /// Base for analyzers that read typed options from .editorconfig.
    /// Handles per-file option lookup, caching, and initialization boilerplate.
    /// </summary>
    internal abstract class ConfigurableAnalyzer<TOptions> : DiagnosticAnalyzer
    {
        protected sealed class OptionsAccessor
        {
            private readonly AnalyzerConfigOptionsProvider _provider;
            private readonly Func<AnalyzerConfigOptions, TOptions> _parse;
            private readonly TOptions _defaults;
            private readonly ConcurrentDictionary<SyntaxTree, TOptions> _cache = new();

            public OptionsAccessor(
                AnalyzerConfigOptionsProvider provider,
                Func<AnalyzerConfigOptions, TOptions> parse,
                TOptions defaults)
            {
                _provider = provider;
                _parse = parse;
                _defaults = defaults;
            }

            public TOptions ForNode(in SyntaxNodeAnalysisContext context) =>
                ForTree(context.Node.SyntaxTree);

            public TOptions ForSymbol(in SymbolAnalysisContext context)
            {
                var tree = context.Symbol.Locations.FirstOrDefault()?.SourceTree;
                return tree is null ? _defaults : ForTree(tree);
            }

            private TOptions ForTree(SyntaxTree tree) =>
                _cache.GetOrAdd(tree, t => _parse(_provider.GetOptions(t)));
        }

        /// <summary>Return strongly typed defaults used when no config is present or invalid.</summary>
        protected abstract TOptions Defaults { get; }

        /// <summary>Parse options for a single file from AnalyzerConfigOptions.</summary>
        protected abstract TOptions Parse(AnalyzerConfigOptions opts);

        /// <summary>Register syntax and symbol actions. Use the provided accessor to get options.</summary>
        protected abstract void Register(CompilationStartAnalysisContext start, OptionsAccessor options);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(start =>
            {
                var provider = start.Options.AnalyzerConfigOptionsProvider;
                var accessor = new OptionsAccessor(provider, Parse, Defaults);
                Register(start, accessor);
            });
        }
    }
}
