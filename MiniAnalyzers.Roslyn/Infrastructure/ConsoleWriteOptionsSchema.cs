using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure;

internal sealed class ConsoleWriteOptionsSchema : IOptionSchema<ConsoleWriteOptions>
{
    public string DiagnosticId => "MNA0003";
    public ConsoleWriteOptions Defaults => new();

    public ConsoleWriteOptions Bind(AnalyzerConfigOptions opts, Compilation compilation)
    {
        // Use the same robust bool parsing you already use elsewhere.
        bool allowTop = OptionReaders.ReadBool(opts, Key("allow_in_top_level"), Defaults.AllowInTopLevel);
        bool allowTests = OptionReaders.ReadBool(opts, Key("allow_in_tests"), Defaults.AllowInTests);
        var prefix = OptionReaders.TryGet(opts, Key("required_prefix"), out var raw) ? raw?.Trim() ?? "" : "";
        var ignore = OptionReaders.ReadBool(opts, Key("required_prefix_ignore_case"), Defaults.RequiredPrefixIgnoreCase);
        return new ConsoleWriteOptions(allowTop, allowTests, prefix, ignore);

    }

    private string Key(string name) => $"dotnet_diagnostic.{DiagnosticId}.{name}";

}
