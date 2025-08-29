using System.Collections.Immutable;

namespace MiniAnalyzers.Roslyn.Infrastructure.Options.EmptyCatch;

internal sealed record EmptyCatchOptions(
    bool IgnoreCancellation = true,
    bool TreatEmptyStatementAsEmpty = true,
    IImmutableSet<string>? AllowedExceptionTypes = null);
