namespace MiniAnalyzers.Roslyn.Infrastructure;

internal sealed record AsyncVoidOptions(
    bool AllowEventHandlers = true,
    bool CheckAnonymousDelegates = true);