using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure
{
    /// <summary>
    /// Binds .editorconfig for MNA0004 to a WeakVarOptions snapshot.
    /// Supported:
    ///   dotnet_diagnostic.MNA0004.min_length    = int (1..50)
    ///   dotnet_diagnostic.MNA0004.allowed_names = csv (e.g. i,j,k,tmp)
    ///   dotnet_diagnostic.MNA0004.weak_names    = csv
    ///   dotnet_diagnostic.MNA0004.check_foreach = bool
    /// </summary>
    internal sealed class WeakVarOptionsSchema : IOptionSchema<WeakVarOptions>
    {
        public string DiagnosticId => "MNA0004";
        public WeakVarOptions Defaults => new();

        public WeakVarOptions Bind(AnalyzerConfigOptions opts, Compilation compilation)
        {
            int minLen = ReadInt(opts, Key("min_length"), Defaults.MinLength, 1, 50);
            var allowed = ReadSet(opts, Key("allowed_names"));
            var weak = ReadSet(opts, Key("weak_names"));
            bool checkForeach = ReadBool(opts, Key("check_foreach"), Defaults.CheckForeach);

            return new WeakVarOptions(
                MinLength: minLen,
                AllowedNames: allowed,
                WeakNames: weak,
                CheckForeach: checkForeach);
        }

        private string Key(string name) => $"dotnet_diagnostic.{DiagnosticId}.{name}";

        private static int ReadInt(AnalyzerConfigOptions o, string key, int def, int min, int max)
        {
            if (!o.TryGetValue(key, out var raw) || !int.TryParse(raw, out var n)) return def;
            if (n < min) n = min;
            if (n > max) n = max;
            return n;
        }

        private static bool ReadBool(AnalyzerConfigOptions o, string key, bool def)
        {
            if (!TryGet(o, key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return def;

            // Normalize casing + trim
            raw = raw.Trim().ToLowerInvariant();

            return raw switch
            {
                "true" => true,
                "false" => false,
                "1" => true,
                "0" => false,
                _ => def
            };
        }

        private static IImmutableSet<string> ReadSet(AnalyzerConfigOptions o, string key)
        {
            if (!TryGet(o, key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return ImmutableHashSet<string>.Empty;

            // Accept commas OR semicolons; trim whitespace and quotes.
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim().Trim('"', '\''))
                           .Where(s => s.Length > 0);

            return parts.ToImmutableHashSet(StringComparer.Ordinal);
        }
        private static bool TryGet(AnalyzerConfigOptions o, string key, out string raw)
        {
            // Some MSBuild/EditorConfig pipelines normalize keys to lowercase.
            if (o.TryGetValue(key, out raw))
                return true;
            if (o.TryGetValue(key.ToLowerInvariant(), out raw))
                return true;
            raw = string.Empty;
            return false;
        }

    }

}
