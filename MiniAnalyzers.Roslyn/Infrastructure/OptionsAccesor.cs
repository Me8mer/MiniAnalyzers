using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure
{
    /// <summary>
    /// Per-analyzer schema that knows defaults and how to bind .editorconfig to a typed options snapshot.
    /// </summary>
    internal interface IOptionSchema<TOptions>
    {
        string DiagnosticId { get; }
        TOptions Defaults { get; }
        TOptions Bind(AnalyzerConfigOptions opts, Compilation compilation);
    }

    /// <summary>
    /// Caches typed options snapshots per SyntaxTree for a given schema.
    /// </summary>
    internal sealed class OptionsAccessor<TOptions>
    {
        private readonly AnalyzerConfigOptionsProvider _provider;
        private readonly Compilation _compilation;
        private readonly IOptionSchema<TOptions> _schema;
        private readonly ConcurrentDictionary<SyntaxTree, TOptions> _cache = new();

        public OptionsAccessor(AnalyzerConfigOptionsProvider provider, Compilation compilation, IOptionSchema<TOptions> schema)
        {
            _provider = provider;
            _compilation = compilation;
            _schema = schema;
        }

        public TOptions ForTree(SyntaxTree tree)
        {
            return _cache.GetOrAdd(tree, t =>
            {
                var opts = _provider.GetOptions(t);
                return _schema.Bind(opts, _compilation);
            });
        }

        public TOptions ForSymbol(in SymbolAnalysisContext context)
        {
            var tree = context.Symbol.Locations.FirstOrDefault()?.SourceTree;
            return tree is null ? _schema.Defaults : ForTree(tree);
        }
    }
}
