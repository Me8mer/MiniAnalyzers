namespace MiniAnalyzers.Roslyn.Infrastructure.Options.ConsoleWrite;

internal sealed record ConsoleWriteOptions(
    bool AllowInTopLevel = false,
    bool AllowInTests = true,
    string RequiredPrefix = "",
    bool RequiredPrefixIgnoreCase = true);
