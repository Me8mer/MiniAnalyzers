using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using MiniAnalyzers.Roslyn.Infrastructure.Common;

namespace MiniAnalyzers.Roslyn.Infrastructure.Options.AsyncVoid;

internal sealed class AsyncVoidOptionsSchema : IOptionSchema<AsyncVoidOptions>
{
    public string DiagnosticId => "MNA0001";
    public AsyncVoidOptions Defaults => new();

    public AsyncVoidOptions Bind(AnalyzerConfigOptions opts, Compilation compilation)
    {
        bool allowHandlers = OptionReaders.ReadBool(opts, Key("allow_event_handlers"), Defaults.AllowEventHandlers);
        bool checkAnon = OptionReaders.ReadBool(opts, Key("check_anonymous_delegates"), Defaults.CheckAnonymousDelegates);
        return new AsyncVoidOptions(allowHandlers, checkAnon);
    }

    private string Key(string name) => $"dotnet_diagnostic.{DiagnosticId}.{name}";
}
