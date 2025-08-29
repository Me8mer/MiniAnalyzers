using System.Collections.Immutable;

namespace MiniAnalyzers.Roslyn.Infrastructure;

internal sealed record EmptyCatchOptions(
    bool IgnoreCancellation = true,
    bool TreatEmptyStatementAsEmpty = true,
    IImmutableSet<string>? AllowedExceptionTypes = null);
