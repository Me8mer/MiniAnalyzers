namespace MiniAnalyzers.Core;

/// <summary>
/// Simple DTO produced by the analysis step and consumed by the UI.
/// Represents a single analyzer diagnostic in a user friendly form.
/// </summary>
public sealed class DiagnosticInfo
{
    /// <summary>Diagnostic ID, for example MNA0001.</summary>
    public string Id { get; init; } = "";

    /// <summary>Severity as a string, for example Warning or Error.</summary>
    public string Severity { get; init; } = "";

    /// <summary>Human readable diagnostic message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Absolute file path that contains the diagnostic.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>One based line number of the primary location.</summary>
    public int Line { get; init; }

    /// <summary>One based column number of the primary location.</summary>
    public int Column { get; init; }

    /// <summary>Short analyzer title that produced the diagnostic.</summary>
    public string Analyzer { get; init; } = "";

    /// <summary>Name of the project where the diagnostic was found.</summary>
    public string ProjectName { get; init; } = "";

    /// <summary>Optional fix recommendation coming from the analyzer (Diagnostic.Properties["Suggestion"]).</summary>
    public string? Suggestion { get; init; }

    /// <summary>
    /// Preformatted code excerpt around the location.
    /// The exact span is marked with [| and |].
    /// </summary>
    public string? ContextSnippet { get; init; }


}
