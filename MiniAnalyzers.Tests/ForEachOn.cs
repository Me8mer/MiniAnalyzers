//using System;
//using System.Collections.Immutable;
//using System.Threading.Tasks;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using MiniAnalyzers.Roslyn.Analyzers;
//using MiniAnalyzers.Roslyn.Infrastructure;
//using MiniAnalyzers.Tests.Verifiers;

//namespace MiniAnalyzers.Tests.EditorConfig.WeakVar
//{
//    [TestClass]
//    public sealed class WeakVariableNameAnalyzer_ManualOptionsTests
//    {
//        [TestCleanup]
//        public void Cleanup() => OptionTestHooks.Reset();

//        // Toggle foreach checking per test, no files needed.
//        [TestMethod]
//        [DataRow(true, true)]   // check_foreach=true => expect diagnostic
//        [DataRow(false, false)]  // check_foreach=false => expect no diagnostic
//        public async Task CheckForeach_GlobalToggle(bool checkForeach, bool expectDiagnostic)
//        {
//            OptionTestHooks.Global = new WeakVarOptions(
//                MinLength: 3, // keep default schema values!
//                AllowedNames: ImmutableHashSet<string>.Empty,
//                WeakNames: ImmutableHashSet<string>.Empty,
//                CheckForeach: checkForeach);

//            var code = expectDiagnostic
//                ? @"class C { void M() { foreach (var [|i|] in new int[0]) { } } }"
//                : @"class C { void M() { foreach (var i in new int[0]) { } } }";

//            await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
//        }

//        // Override min_length per test.
//        [TestMethod]
//        [DataRow(3, false)] // default OK, 'foo' length 3 => no diagnostic
//        [DataRow(4, true)]  // tightened to 4 => 'foo' flagged
//        public async Task MinLength_Override(int minLength, bool expectDiagnostic)
//        {
//            OptionTestHooks.Global = new WeakVarOptions(MinLength: minLength);

//            var code = expectDiagnostic
//                ? @"class C { void M() { int [|foo|] = 0; } }"
//                : @"class C { void M() { int foo = 0; } }";

//            await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
//        }

//        // Allow-list and weak-list from options, no .editorconfig.
//        [TestMethod]
//        [DataRow("aa", false)]     // allowed => suppressed
//        [DataRow("potato", true)]  // forced weak => flagged even though long
//        public async Task Allowed_And_Weak_Names(string name, bool expectDiagnostic)
//        {
//            OptionTestHooks.Global = new WeakVarOptions(
//                AllowedNames: ImmutableHashSet.Create("aa"),
//                WeakNames: ImmutableHashSet.Create("potato"));

//            var code = expectDiagnostic
//                ? $@"class C {{ void M() {{ int [|{name}|] = 0; }} }}"
//                : $@"class C {{ void M() {{ int {name} = 0; }} }}";

//            await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
//        }

//        // If you ever need per-file behavior inside a single test, use PerFile:
//        [TestMethod]
//        public async Task PerFile_Toggle_Foreach()
//        {
//            OptionTestHooks.PerFile = path =>
//                path?.EndsWith("On.cs", StringComparison.Ordinal) == true
//                    ? new WeakVarOptions(CheckForeach: true)
//                    : new WeakVarOptions(CheckForeach: false);

//            var test = new CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.Test();
//            test.TestState.Sources.Add(("Off.cs", "class A { void M() { foreach (var i in new int[0]) { } } }"));
//            test.TestState.Sources.Add(("On.cs", "class B { void M() { foreach (var [|i|] in new int[0]) { } } }"));
//            await test.RunAsync();
//        }
//    }
//}
