using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers.Core;
using MiniAnalyzers.Roslyn.Analyzers;
using MiniAnalyzers.Tests.Verifiers;
using System.Threading.Tasks;

namespace MiniAnalyzers.Tests;

[TestClass]
public sealed class AsyncVoidAnalyzerTests
{
    [TestMethod]
    public async Task FlagsAsyncVoidMethod()
    {
        var code = @"
            using System.Threading.Tasks;
            class C
            {
                public async [|void|] M()
                {
                    await Task.Yield();
                }
            }";

        await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>
            .VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlagEventHandler()
    {
        var code = @"
            using System;
            using System.Threading.Tasks;
            class UI
            {
                public async void OnClick(object sender, EventArgs e)
                {
                    await Task.Yield();
                }
            }";
        await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>
            .VerifyAnalyzerAsync(code);
    }

    [TestMethod]
        public async Task FlagsAsyncVoidLocalFunction()
        {
            var code = @"
                using System.Threading.Tasks;
                class C
                {
                    public void Outer()
                    {
                        async [|void|] Inner()
                        {
                            await Task.Yield();
                        }
                        Inner();
                    }
                }";
            await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
        }

    [TestMethod]
    public async Task FlagsAsyncLambdaAssignedToAction()
    {
        var code = @"
            using System;
            using System.Threading.Tasks;
            class C
            {
                public void M()
                {
                   Action a = [|async () => { await Task.Yield(); }|];
                }
            }";
        await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
        public async Task DoesNotFlagAsyncLambdaAssignedToFuncTask()
        {
            var code = @"
                using System;
                using System.Threading.Tasks;
                class C
                {
                    public void M()
                    {
                        Func<Task> f = async () => { await Task.Yield(); };
                    }
                }";
            await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
        }

        [TestMethod]
        public async Task DoesNotFlagAsyncLambdaAsEventHandler()
        {
            var code = @"
                using System;
                using System.Threading.Tasks;
                class UI
                {
                    public UI()
                    {
                        EventHandler h = async (s, e) => { await Task.Yield(); };
                    }
                }";
            await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
        }
    [TestMethod]
    public async Task DoesNotFlagOverrideAsyncVoidMethod()
    {
        var code = @"
            using System.Threading.Tasks;

            abstract class Base
            {
                public abstract void M(); // base forces 'void'
            }

            class C : Base
            {
                public override async void M() // should be skipped (override)
                {
                    await Task.Yield();
                }
            }";
        await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task FlagsAsyncAnonymousMethodAssignedToAction()
    {
        var code = @"
        using System;
        using System.Threading.Tasks;

        class C
        {
            public void M()
            {
                Action a = [|async delegate { await Task.Yield(); }|]; // async void via Action
            }
        }";
        await CSharpAnalyzerVerifier<AsyncVoidAnalyzer>.VerifyAnalyzerAsync(code);
    }



}
