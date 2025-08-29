using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniAnalyzers.Roslyn.Infrastructure;

internal sealed class EmptyCatchOptionsSchema : IOptionSchema<EmptyCatchOptions>
{
    public string DiagnosticId => "MNA0002";
    public EmptyCatchOptions Defaults => new();

    public EmptyCatchOptions Bind(AnalyzerConfigOptions opts, Compilation compilation)
    {
        bool ignoreCancel = OptionReaders.ReadBool(opts, Key("ignore_cancellation"), Defaults.IgnoreCancellation);
        bool treatEmpty = OptionReaders.ReadBool(opts, Key("treat_empty_statement_as_empty"), Defaults.TreatEmptyStatementAsEmpty);
        var allowed = OptionReaders.ReadSet(opts, Key("allowed_exception_types"));

        // Normalize empty set to null for a tiny allocation win.
        var allowedOrNull = allowed.Count == 0 ? null : allowed;

        return new EmptyCatchOptions(ignoreCancel, treatEmpty, allowedOrNull);
    }

    private string Key(string name) => $"dotnet_diagnostic.{DiagnosticId}.{name}";
}
