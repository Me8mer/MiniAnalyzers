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
/// <summary>
/// Runs Roslyn analyzers over a solution or a project and returns
/// only source-based diagnostics enriched for the UI (project name,
/// file path, location, and a context snippet).
/// </summary>
public static class AnalysisRunner
{
    /// <summary>
    /// Opens a solution and executes the given analyzers for all C# projects.
    /// Returns only analyzer diagnostics with source locations.
    /// </summary>
    /// <param name="solutionPath">Absolute path to a .sln file.</param>
    /// <param name="analyzers">Analyzers to run.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <param name="contextLines">
    /// Number of context lines to include above and below the primary diagnostic line.
    /// Defaults to 2 (2 up, 2 down).
    /// </param>
    /// <returns>Flat list of diagnostics enriched with project and file info.</returns>
    public static async Task<IReadOnlyList<DiagnosticInfo>> AnalyzeSolutionAsync(
         string solutionPath,
         IEnumerable<DiagnosticAnalyzer> analyzers,
         CancellationToken cancellationToken = default,
          int contextLines = 2)
    {
        RegisterMSBuildIfNeeded();

        using var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionPath, null, cancellationToken);
        var results = new List<DiagnosticInfo>();

        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null) continue;

            var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray(), project.AnalyzerOptions);
            var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

            foreach (var diagnostic in diags.Where(diag => diag.Location.IsInSource))
            {
                results.Add(ToDiagnosticInfo(diagnostic, project.Name, cancellationToken, contextLines));
            }
        }
        return results;

    }

    /// <summary>
    /// Maps a Roslyn <see cref="Microsoft.CodeAnalysis.Diagnostic"/> to a <see cref="DiagnosticInfo"/>.
    /// Also builds a formatted code snippet for quick context preview.
    /// </summary>
    /// <param name="diagnostic">Source based diagnostic.</param>
    /// <param name="projectName">Owning project name.</param>
    /// <param name="ct">Cooperative cancellation token.</param>
    /// <param name="contextLines">Number of context lines to include in the snippet.</param>
    /// <returns>Populated <see cref="DiagnosticInfo"/> ready for UI binding.</returns>
    private static DiagnosticInfo ToDiagnosticInfo(Diagnostic diagnostic, string projectName, CancellationToken ct, int contextLines)
    {
        var span = diagnostic.Location.GetLineSpan();
        diagnostic.Properties.TryGetValue("Suggestion", out var suggestion);
        return new DiagnosticInfo
        {
            Id = diagnostic.Id,
            Severity = diagnostic.Severity.ToString(),
            Message = diagnostic.GetMessage(),
            FilePath = span.Path,
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1,
            Analyzer = diagnostic.Descriptor.Title.ToString(),
            ProjectName = projectName,
            ContextSnippet = ContextSnippetBuilder.Build(diagnostic.Location, ct, contextLines),
            Suggestion = suggestion
        };
    }

    /// <summary>
    /// Opens a single CSharp project and executes the given analyzers.
    /// Returns only analyzer diagnostics with source locations.
    /// </summary>
    /// <param name="projectPath">Absolute path to a .csproj file.</param>
    /// <param name="analyzers">Analyzers to run.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <param name="contextLines">Number of context lines to include in the snippet.</param>
    /// <returns>Flat list of diagnostics enriched with project and file info.</returns>
    public static async Task<IReadOnlyList<DiagnosticInfo>> AnalyzeProjectAsync(
        string projectPath,
        IEnumerable<DiagnosticAnalyzer> analyzers,
        CancellationToken cancellationToken = default,
        int contextLines = 2)
    {
        RegisterMSBuildIfNeeded();

        using var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
        var compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null) return Array.Empty<DiagnosticInfo>();

        var withAnalyzers = compilation.WithAnalyzers(analyzers.ToImmutableArray(), project.AnalyzerOptions);
        var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);

        var list = new List<DiagnosticInfo>();
        foreach (var diagnostic in diags.Where(diag => diag.Location.IsInSource))
        {
            list.Add(ToDiagnosticInfo(diagnostic, project.Name, cancellationToken, contextLines));
        }

        return list;
    }

   

    /// <summary>
    /// Registers an MSBuild toolset for Roslyn if none is registered yet.
    /// Uses the default Visual Studio installation on Windows which fits VS 2022.
    /// </summary>
    private static void RegisterMSBuildIfNeeded()
    {
        if (!MSBuildLocator.IsRegistered)
        {
            // Uses the installed Visual Studio 2022 toolset on Windows, which fits your environment.
            MSBuildLocator.RegisterDefaults();
        }
    }
}
