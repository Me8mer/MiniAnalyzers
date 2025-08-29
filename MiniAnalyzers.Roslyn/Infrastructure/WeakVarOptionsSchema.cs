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
        private string Key(string name) => $"dotnet_diagnostic.{DiagnosticId}.{name}";


        public WeakVarOptions Bind(AnalyzerConfigOptions opts, Compilation compilation)
        {
            int minLen = OptionReaders.ReadInt(opts, Key("min_length"), Defaults.MinLength, 1, 50);
            var allowed = OptionReaders.ReadSet(opts, Key("allowed_names"));
            var weak = OptionReaders.ReadSet(opts, Key("weak_names"));
            bool checkForeach = OptionReaders.ReadBool(opts, Key("check_foreach"), Defaults.CheckForeach);

            return new WeakVarOptions(
                MinLength: minLen,
                AllowedNames: allowed,
                WeakNames: weak,
                CheckForeach: checkForeach);
        }


    }

}
