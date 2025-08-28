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

    //[TestMethod]
    //[DataRow("WeakVarForeach_Off", 0)]  // foreach checks disabled
    //public async Task WeakVar_Foreach_Toggle(string sample, int expectedCount)
    //{
    //    var project = Path.Combine(RepoRoot(), "Samples", sample, $"{sample}.csproj");
    //    var analyzers = new DiagnosticAnalyzer[] { new WeakVariableNameAnalyzer() };

    //    var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);
    //    var count = results.Count(r => r.Id == WeakVariableNameAnalyzer.DiagnosticId);
    //    Assert.AreEqual(expectedCount, count, $"Unexpected MNA0004 count in {sample}.");
    //}

    [TestMethod]
    public async Task MNA0004_AllKeys_Visible()
    {
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

        var projectPath = Path.Combine(RepoRoot(), "Samples", "WeakVarForeach_On", "WeakVarForeach_On.csproj");
        using var ws = MSBuildWorkspace.Create();
        var project = await ws.OpenProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();
        var tree = compilation!.SyntaxTrees.Single(t => Path.GetFileName(t.FilePath) == "Program.cs");

        var o = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);

        Assert.IsTrue(o.TryGetValue("dotnet_diagnostic.MNA0004.min_length", out var min) && min == "4");
        Assert.IsTrue(o.TryGetValue("dotnet_diagnostic.MNA0004.allowed_names", out var allowed) && allowed.Contains("id"));
        Assert.IsTrue(o.TryGetValue("dotnet_diagnostic.MNA0004.weak_names", out var weak) && weak.Contains("foo"));
        Assert.IsTrue(o.TryGetValue("dotnet_diagnostic.MNA0004.check_foreach", out var cf) && cf.ToLowerInvariant() == "true");
    }

    [TestMethod]
    [DataRow("WeakVarForeach_Off", 0)]
    public async Task WeakVar_Foreach_Toggle(string sample, int expectedCount)
    {
        var project = Path.Combine(RepoRoot(), "Samples", sample, $"{sample}.csproj");
        var analyzers = new DiagnosticAnalyzer[] { new WeakVariableNameAnalyzer() };

        var results = await AnalysisRunner.AnalyzeProjectAsync(project, analyzers);
        var count = results.Count(r => r.Id == WeakVariableNameAnalyzer.DiagnosticId);
        Assert.AreEqual(expectedCount, count, $"Unexpected MNA0004 count in {sample}.");
    }
}
