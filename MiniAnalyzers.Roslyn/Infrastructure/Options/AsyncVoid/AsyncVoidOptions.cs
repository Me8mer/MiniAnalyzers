namespace MiniAnalyzers.Roslyn.Infrastructure.Options.AsyncVoid;

internal sealed record AsyncVoidOptions(
    bool AllowEventHandlers = true,
    bool CheckAnonymousDelegates = true);