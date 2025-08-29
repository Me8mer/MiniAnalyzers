using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers.Core;
using MiniAnalyzers.Roslyn.Analyzers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MiniAnalyzers.Tests
{
    [TestClass]
    public partial class BigIntegrationTest
    {
        private static string RepoRoot()
        {
            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            return Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", ".."));
        }

        [TestMethod]
        public async Task AllAnalyzersSolution_Integrated_Aggregates_Across_Projects()
        {
            var root = Path.Combine(RepoRoot(), "Samples", "AllAnalyzersSolution");

            var projects = new[]
            {
                Path.Combine(root, "AllAnalyzers.App",   "AllAnalyzers.App.csproj"),
                Path.Combine(root, "AllAnalyzers.Lib",   "AllAnalyzers.Lib.csproj"),
                Path.Combine(root, "AllAnalyzers.Tests", "AllAnalyzers.Tests.csproj"),
            };

            var analyzers = new DiagnosticAnalyzer[]
            {
                new AsyncVoidAnalyzer(),        // MNA0001
                new EmptyCatchBlockAnalyzer(),  // MNA0002
                new ConsoleWriteLineAnalyzer(), // MNA0003 (+ MNA0003A)
                new WeakVariableNameAnalyzer()  // MNA0004
            };

            var all = (await Task.WhenAll(projects.Select(p => AnalysisRunner.AnalyzeProjectAsync(p, analyzers))))
                      .SelectMany(x => x)
                      .ToList();

            var mna3 = all.Where(d => d.Id == "MNA0003").ToList();
            var mna3a = all.Where(d => d.Id == "MNA0003A").ToList();

            // Exact expected counts from the sample code above:
            // App:   MNA0001=1, MNA0002=1, MNA0003A=1, MNA0003=1, MNA0004=2
            // Lib:   MNA0001=1,                MNA0003=1,           MNA0004=1
            // Tests: (Console allowed) -> 0
            Assert.AreEqual(2, all.Count(d => d.Id == "MNA0001"), "MNA0001 total mismatch.");
            Assert.AreEqual(1, all.Count(d => d.Id == "MNA0002"), "MNA0002 total mismatch.");
            Assert.AreEqual(2, mna3.Count, "MNA0003 total mismatch.");
            Assert.AreEqual(1, mna3a.Count, "MNA0003A total mismatch.");
            Assert.AreEqual(1, all.Count(d => d.Id == "MNA0003A"), "MNA0003A total mismatch.");
            Assert.AreEqual(3, all.Count(d => d.Id == "MNA0004"), "MNA0004 total mismatch.");

            // Severity checks from .editorconfig (Error/Warning/Suggestion)
            // We only spot-check one example per ID.
            Assert.IsTrue(all.Any(d => d.Id == "MNA0002" && d.Severity == "Error"), "MNA0002 should be Error.");
            Assert.IsTrue(mna3.All(d => d.Severity == "Warning"), "MNA0003 should be warning..");
            Assert.IsTrue(mna3a.All(d => d.Severity == "Info"), "MNA0003A should be Suggestion.");
            Assert.IsTrue(all.Any(d => d.Id == "MNA0001" && d.Severity == "Warning"), "MNA0001 should be Warning.");
            Assert.IsTrue(all.Any(d => d.Id == "MNA0004" && d.Severity == "Warning"), "MNA0004 should be Warning.");
        }
    }
}
