namespace MiniAnalyzers.Roslyn.Infrastructure;

internal sealed record ConsoleWriteOptions(
    bool AllowInTopLevel = false,
    bool AllowInTests = true,
    string RequiredPrefix = "",
    bool RequiredPrefixIgnoreCase = true);
