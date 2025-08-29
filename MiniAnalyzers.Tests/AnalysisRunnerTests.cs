using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers;
using MiniAnalyzers.Core;
using MiniAnalyzers.Roslyn.Analyzers;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace MiniAnalyzers.Tests;

[TestClass]
public class AnalysisRunnerTests
{

    private static string RepoRoot()
    {
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        return Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", ".."));
    }

    [TestMethod]
    public async Task FindsAsyncVoidDiagnosticInSampleProject()
    {
        // Navigate back up from bin/Debug/net9.0/ to the repo root
        // Now point into Samples
        var projectPath = Path.Combine(RepoRoot(), "Samples", "AsyncVoidProject", "AsyncVoidProject.csproj");

        var analyzers = new DiagnosticAnalyzer[]
        {
            new AsyncVoidAnalyzer()
        };

        // Act
        var results = await AnalysisRunner.AnalyzeProjectAsync(projectPath, analyzers);

        // Assert
        Assert.IsTrue(results.Any(r => r.Id == "MNA0001"),
            "Expected AsyncVoidAnalyzer to flag MNA0001.");
    }
    [TestMethod]
    public async Task AsyncVoidProject_ExactlyOne_MNA0001()
    {
        // Analyzer under test: AsyncVoid (MNA0001)
        var project = Path.Combine(RepoRoot(), "Samples", "AsyncVoidProject", "AsyncVoidProject.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new AsyncVoidAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);

        // Exactly one async-void occurrence expected
        var count = results.Count(r => r.Id == AsyncVoidAnalyzer.DiagnosticId);
        Assert.AreEqual(1, count, "Expected exactly one MNA0001 in AsyncVoidProject.");
    }

    [TestMethod]
    public async Task EmptyCatchProject_ExactlyOne_MNA0002()
    {
        // Analyzer under test: Empty catch (MNA0002)
        var project = Path.Combine(RepoRoot(), "Samples", "EmptyCatchProject", "EmptyCatchProject.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new EmptyCatchBlockAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);

        // One empty 'catch' should be flagged; the OCE catch should be ignored
        var count = results.Count(r => r.Id == EmptyCatchBlockAnalyzer.DiagnosticId);
        Assert.AreEqual(1, count, "Expected exactly one MNA0002 in EmptyCatchProject.");
    }


    [TestMethod]
    public async Task MNA0004_AllKeys_Visible()
    {
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

        var projectPath = Path.Combine(RepoRoot(), "Samples", "WeakVarForeach_On", "WeakVarForeach_On.csproj");
        using var workSpace = MSBuildWorkspace.Create();
        var project = await workSpace.OpenProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();
        var tree = compilation!.SyntaxTrees.Single(t => Path.GetFileName(t.FilePath) == "Program.cs");

        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);

        Assert.IsTrue(options.TryGetValue("dotnet_diagnostic.MNA0004.min_length", out var min) && min == "4");
        Assert.IsTrue(options.TryGetValue("dotnet_diagnostic.MNA0004.allowed_names", out var allowed) && allowed.Contains("id"));
        Assert.IsTrue(options.TryGetValue("dotnet_diagnostic.MNA0004.weak_names", out var weak) && weak.Contains("foo"));
        Assert.IsTrue(options.TryGetValue("dotnet_diagnostic.MNA0004.check_foreach", out var checkForeach) && checkForeach.ToLowerInvariant() == "true");
    }

    [TestMethod]
    [DataRow("WeakVarForeach_Off", 0)]
    public async Task WeakVar_Foreach_Toggle(string sample, int expectedCount)
    {
        var project = Path.Combine(RepoRoot(), "Samples", sample, $"{sample}.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new WeakVariableNameAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);
        var count = results.Count(result => result.Id == WeakVariableNameAnalyzer.DiagnosticId);
        Assert.AreEqual(expectedCount, count, $"Unexpected MNA0004 count in {sample}.");
    }

    [TestMethod]
    public async Task MixedIssuesProject_Finds_All_Analyzers()
    {
        var projectPath = Path.Combine(RepoRoot(), "Samples", "MixedIssuesProject", "MixedIssuesProject.csproj");

        var analyzers = new DiagnosticAnalyzer[]
        {
        new AsyncVoidAnalyzer(),
        new EmptyCatchBlockAnalyzer(),
        new WeakVariableNameAnalyzer()
        };

        var results = await AnalysisRunner.AnalyzeProjectAsync(projectPath, analyzers);

        Assert.IsTrue(results.Any(r => r.Id == AsyncVoidAnalyzer.DiagnosticId), "Expected at least one MNA0001.");
        Assert.IsTrue(results.Any(r => r.Id == EmptyCatchBlockAnalyzer.DiagnosticId), "Expected at least one MNA0002.");
        Assert.IsTrue(results.Any(r => r.Id == WeakVariableNameAnalyzer.DiagnosticId), "Expected at least one MNA0004.");
    }

    [TestMethod]
    [DataRow(AsyncVoidAnalyzer.DiagnosticId, 2)]
    [DataRow(EmptyCatchBlockAnalyzer.DiagnosticId, 1)]
    [DataRow(WeakVariableNameAnalyzer.DiagnosticId, 5)]
    public async Task MixedIssuesProject_Exact_Diagnostic_Counts(string diagnosticId, int expectedCount)
    {
        var projectPath = Path.Combine(RepoRoot(), "Samples", "MixedIssuesProject", "MixedIssuesProject.csproj");

        var analyzers = new DiagnosticAnalyzer[]
        {
        new AsyncVoidAnalyzer(),
        new EmptyCatchBlockAnalyzer(),
        new WeakVariableNameAnalyzer()
        };

        var results = await AnalysisRunner.AnalyzeProjectAsync(projectPath, analyzers);

        var count = results.Count(r => r.Id == diagnosticId);
        Assert.AreEqual(expectedCount, count, $"Unexpected count for {diagnosticId}.");
    }
    [TestMethod]
    [DataRow("AsyncVoidProject", 1)]
    public async Task AsyncVoid_EventHandler_Toggle_By_EditorConfig(string sample, int expectedCount)
    {
        var project = Path.Combine(RepoRoot(), "Samples", sample, $"{sample}.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new AsyncVoidAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);
        var count = results.Count(r => r.Id == AsyncVoidAnalyzer.DiagnosticId);

        Assert.AreEqual(expectedCount, count, $"Unexpected {AsyncVoidAnalyzer.DiagnosticId} count in {sample}.");
    }


    [TestMethod]
    [DataRow("MNA0003A", 1)] // one missing-prefix warning
    [DataRow("MNA0003", 1)] // one general Console.Write* warning
    public async Task ConsoleWrite_RequiredPrefix_Project_WithFixedEditorConfig(string diagnosticId, int expectedCount)
    {
        var projectPath = Path.Combine(RepoRoot(), "Samples", "ConsolePrefixProject", "ConsolePrefixProject.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new ConsoleWriteLineAnalyzer() };

        var diagnostics = await AnalysisRunner.AnalyzeProjectAsync(projectPath, analyzers);
        var count = diagnostics.Count(d => d.Id == diagnosticId);

        Assert.AreEqual(expectedCount, count, $"Unexpected {diagnosticId} count in ConsolePrefixProject.");
    }

    [TestMethod]
    public async Task EmptyCatch_ErrorSeverity_Project_HasExactlyOneError()
    {
        var projectPath = Path.Combine(RepoRoot(), "Samples", "EmptyCatch_ErrorSeverity", "EmptyCatch_ErrorSeverity.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new EmptyCatchBlockAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(projectPath, analyzers);

        // Exactly one MNA0002 expected
        var mna2 = results.Where(r => r.Id == EmptyCatchBlockAnalyzer.DiagnosticId).ToList();
        Assert.AreEqual(1, mna2.Count, "Expected exactly one MNA0002 in EmptyCatch_ErrorSeverity.");

        // And its severity should reflect the .editorconfig override
        Assert.AreEqual("Error", mna2[0].Severity, "Expected MNA0002 severity to be Error via .editorconfig override.");
    }


}
