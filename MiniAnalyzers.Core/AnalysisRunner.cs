using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniAnalyzers.Core;

public sealed class DiagnosticInfo
{
    public string Id { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Message { get; init; } = "";
    public string FilePath { get; init; } = "";
    public int Line { get; init; }       // 1-based
    public int Column { get; init; }     // 1-based
    public string Analyzer { get; init; } = "";
    public string ProjectName { get; init; } = "";

    /// <summary>
    /// Code excerpt around the diagnostic location, with the precise span marked using [| |].
    /// Precomputed by the analysis step so UI stays simple.
    /// </summary>
    public string? ContextSnippet { get; set; }
}

public static class AnalysisRunner
{
    /// <summary>
    /// Opens a solution and runs the given analyzers over all compilations.
    /// Returns a flat list of diagnostics that came from analyzers (compiler diagnostics are filtered out).
    /// </summary>
    public static async Task<IReadOnlyList<DiagnosticInfo>> AnalyzeSolutionAsync(
        string solutionPath,
        IEnumerable<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken = default)
    {
        RegisterMSBuildIfNeeded();

        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(
            solutionPath,
            progress: null,
            cancellationToken: cancellationToken);
        var results = new List<DiagnosticInfo>();

        foreach (var project in solution.Projects)
        {
            if (!project.Language.Equals(LanguageNames.CSharp, StringComparison.Ordinal))
                continue;

            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null)
                continue;

            var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray(), project.AnalyzerOptions, cancellationToken);
            var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

            foreach (var d in diags.Where(d => d.Location.IsInSource))
            {
                var span = d.Location.GetLineSpan();
                results.Add(new DiagnosticInfo
                {
                    Id = d.Id,
                    Severity = d.Severity.ToString(),
                    Message = d.GetMessage(),
                    FilePath = span.Path,
                    Line = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1,
                    Analyzer = d.Descriptor.Title.ToString(),
                    ProjectName = project.Name,
                    ContextSnippet = BuildContextSnippet(d.Location, contextLines: 3, cancellationToken)
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Convenience for a single .csproj path when you do not have a .sln.
    /// </summary>
    public static async Task<IReadOnlyList<DiagnosticInfo>> AnalyzeProjectAsync(
        string projectPath,
        IEnumerable<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken = default)
    {
        RegisterMSBuildIfNeeded();

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
            return Array.Empty<DiagnosticInfo>();

        var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray(), project.AnalyzerOptions, cancellationToken);
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

        var list = new List<DiagnosticInfo>();
        foreach (var d in diags.Where(d => d.Location.IsInSource))
        {
            var span = d.Location.GetLineSpan();
            list.Add(new DiagnosticInfo
            {
                Id = d.Id,
                Severity = d.Severity.ToString(),
                Message = d.GetMessage(),
                FilePath = span.Path,
                Line = span.StartLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
                Analyzer = d.Descriptor.Title.ToString(),
                ProjectName = project.Name,
                ContextSnippet = BuildContextSnippet(d.Location, contextLines: 3, cancellationToken)
            });
        }
        return list;
    }
    private static string BuildContextSnippet(Location location, int contextLines = 3, CancellationToken ct = default)
    {
        var tree = location.SourceTree;
        if (tree is null)
            return string.Empty;

        // SourceText from Roslyn to match the analyzed snapshot on disk.
        var text = tree.GetText(ct);

        var lineSpan = location.GetLineSpan();
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(text.Lines.Count - 1, lineSpan.EndLinePosition.Line + contextLines);

        var hlStart = location.SourceSpan.Start; // absolute positions in file
        var hlEnd = location.SourceSpan.End;

        var sb = new StringBuilder();

        // Width for line numbers
        int width = (endLine + 1).ToString().Length;

        for (int i = startLine; i <= endLine; i++)
        {
            var line = text.Lines[i];
            var lineText = line.ToString();

            // Intersect highlight with this line
            bool intersects =
                (hlStart <= line.End && hlEnd >= line.Start);

            if (intersects)
            {
                // Insert [| and |] around the exact span within this line.
                // Compute relative indices to the line start.
                int relStart = Math.Clamp(hlStart - line.Start, 0, lineText.Length);
                int relEnd = Math.Clamp(hlEnd - line.Start, 0, lineText.Length);

                // Ensure start <= end
                if (relEnd < relStart)
                    (relStart, relEnd) = (relEnd, relStart);

                // Insert end marker first to keep indices valid, then start.
                lineText = lineText.Insert(relEnd, "|]").Insert(relStart, "[|");
            }

            // Mark the primary line with a simple indicator, no dashes.
            string indicator = i == lineSpan.StartLinePosition.Line ? ">" : " ";
            sb.Append((i + 1).ToString().PadLeft(width))
              .Append(' ')
              .Append(indicator)
              .Append(' ')
              .AppendLine(lineText);
        }

        return sb.ToString();
    }

    private static void RegisterMSBuildIfNeeded()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            // Uses the installed Visual Studio 2022 toolset on Windows, which fits your environment.
            MSBuildLocator.RegisterDefaults();
        }
    }
}
