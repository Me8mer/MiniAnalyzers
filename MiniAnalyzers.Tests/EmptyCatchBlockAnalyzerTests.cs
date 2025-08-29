using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers.Roslyn.Analyzers;
using MiniAnalyzers.Tests.Verifiers;

namespace MiniAnalyzers.Tests;

[TestClass]
public sealed class EmptyCatchBlockAnalyzerTests
{
    [TestMethod]
    public async Task Flags_EmptyCatch_Basic()
    {
        var code = @"
        class C
        {
            void M()
            {
                try
                {
                    System.Console.WriteLine();
                }
                [|catch|]
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_NonEmptyCatch_Rethrow()
    {
        var code = @"
        using System;
        class C
        {
            void M()
            {
                try { }
                catch (Exception)
                {
                    throw; // not empty
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_EmptyCatch_WithExceptionType()
    {
        var code = @"
        using System;
        class C
        {
            void M()
            {
                try { }
                [|catch|] (Exception)
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_EmptyCatch_WithWhenFilter()
    {
        var code = @"
        class C
        {
            bool ShouldIgnore => true;

            void M()
            {
                try { }
                [|catch|] when (ShouldIgnore)
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_CommentOnly_Block()
    {
        var code = @"
        class C
        {
            void M()
            {
                try { }
                [|catch|]
                {
                    // intentional? still empty => flag
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_NonEmptyCatch_SimpleStatement()
    {
        var code = @"
        using System;
        class C
        {
            void M()
            {
                try { }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message); // not empty
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Mixed_MultipleCatches_OnlyEmptyFlagged()
    {
        var code = @"
        using System;
        class C
        {
            void M()
            {
                try { }
                [|catch|] (InvalidOperationException)
                {
                }
                catch (Exception)
                {
                    throw; // not empty
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_EmptyCatch_WithSemicolonOnly()
    {
        var code = @"
        class C
        {
            void M()
            {
                try { }
                [|catch|]
                {
                    ;
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_EmptyCatch_OperationCanceledException()
    {
        var code = @"
        class C
        {
            void M()
            {
                try { }
                catch (System.OperationCanceledException)
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_EmptyCatch_TaskCanceledException()
    {
        var code = @"
        class C
        {
            void M()
            {
                try { }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }
    [TestMethod]
    public async Task DoesNotFlag_EmptyCatch_SemicolonOnly_OCE()
    {
        var code = @"
        class C
        {
            void M()
            {
                try { }
                catch (System.OperationCanceledException)
                {
                    ;
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_EmptyCatch_CustomDerivedFromOCE()
    {
        var code = @"
        class MyCancel : System.OperationCanceledException { }

        class C
        {
            void M()
            {
                try { }
                catch (MyCancel)
                {
                }
            }
        }";
        await CSharpAnalyzerVerifier<EmptyCatchBlockAnalyzer>.VerifyAnalyzerAsync(code);
    }



}


