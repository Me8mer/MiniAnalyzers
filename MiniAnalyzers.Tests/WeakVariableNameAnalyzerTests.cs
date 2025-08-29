using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers.Roslyn.Analyzers;
using MiniAnalyzers.Tests.Verifiers;

namespace MiniAnalyzers.Tests;

[TestClass]
public sealed class WeakVariableNameAnalyzerTests
{
    // Flags simple short or uninformative names declared as locals.
    [TestMethod]
    [DataRow("a")]
    [DataRow("b1")]
    [DataRow("aa")] // length == 2 should be flagged
    [DataRow("tmp")] // special case by name
    [DataRow("temp")]
    [DataRow("obj")]
    [DataRow("val")]
    public async Task Flags_ShortOrTmp_LocalNames(string name)
    {
        var code = $@"
            class C
            {{
                void M()
                {{
                    int [|{name}|] = 0;
                }}
            }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Does not flag reasonable names, including "_" which we treat as an intentional discard-like identifier.
    [TestMethod]
    [DataRow("count")]    // good descriptive
    [DataRow("isReady")]  // boolean prefix
    [DataRow("customers")]// plural collection
    [DataRow("id")]       // allow-list
    [DataRow("_")]        // discard
    public async Task DoesNotFlag_GoodOrAllowedNames(string name)
    {
        var code = $@"
            class C
            {{
                void M()
                {{
                    var {name} = 0;
                }}
            }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Does not flag classic for-loop counters declared in the loop initializer.
    [TestMethod]
    [DataRow("i")]
    [DataRow("j")]
    [DataRow("k")]
    public async Task DoesNotFlag_ForInitializerCounters(string counter)
    {
        var code = $@"
            class C
            {{
                void M()
                {{
                    for (int {counter} = 0; {counter} < 1; {counter}++) {{ }}
                }}
            }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Flags short names like 'i' when not declared in the for-initializer.
    [TestMethod]
    [DataRow("i")]
    [DataRow("j")]
    [DataRow("k")]
    public async Task Flags_CounterName_OutsideForInitializer(string name)
    {
        var code = $@"
            class C
            {{
                void M()
                {{
                    int [|{name}|] = 0;
                    for ({name} = 0; {name} < 1; {name}++) {{ }}
                }}
            }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Multiple declarators in one statement should each be flagged independently.
    [TestMethod]
    public async Task Flags_MultipleDeclarators()
    {
        var code = @"
            class C
            {
                void M()
                {
                    int [|a|] = 0, [|b1|] = 1;
                }
            }";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Flags weak names when the declared type is boolean.
    // We don't assert the message suffix yet, just that a diagnostic is reported.
    [TestMethod]
    [DataRow("b")]      // short length
    [DataRow("aa")]     // 2-char
    [DataRow("tmp")]    // weak token
    [DataRow("val")]    // weak token
    public async Task Flags_WeakNames_WithBooleanType(string name)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                bool [|{name}|] = default;
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Does not flag good boolean naming patterns (is/has/can).
    [TestMethod]
    [DataRow("isReady")]
    [DataRow("hasItems")]
    [DataRow("canExecute")]
    [DataRow("shouldRun")]
    [DataRow("wasProcessed")]
    public async Task DoesNotFlag_GoodBooleanPatternNames(string name)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                bool {name} = false;
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Flags weak names when the declared type is a collection or array.
    // Uses fully-qualified names to avoid extra usings.
    [TestMethod]
    [DataRow("int[]", "tmp")]
    [DataRow("System.Collections.Generic.List<int>", "data")]
    [DataRow("System.Collections.Generic.Dictionary<int,int>", "item")]
    [DataRow("System.Collections.Generic.HashSet<int>", "val")]
    public async Task Flags_WeakNames_WithCollections(string type, string name)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                {type} [|{name}|] = default;
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Does not flag decent plural-ish names for collections.
    [TestMethod]
    [DataRow("int[]", "values")]
    [DataRow("System.Collections.Generic.List<int>", "numbers")]
    [DataRow("System.Collections.Generic.Dictionary<int,int>", "mapping")]
    [DataRow("System.Collections.Generic.Queue<int>", "queue")]
    public async Task DoesNotFlag_GoodCollectionNames(string type, string name)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                {type} {name} = default;
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }
    // === Fields ===
    // Weak field names should be flagged; const fields are skipped.
    [TestMethod]
    [DataRow("tmp")]
    [DataRow("a")]
    [DataRow("data")]
    public async Task Flags_WeakFieldNames(string name)
    {
        var code = $@"
        class C
        {{
            private int [|{name}|];
            private const int PI = 3; // should be ignored
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("ct")]
    [DataRow("ok")]
    public async Task DoesNotFlag_AllowedFieldNames(string name)
    {
        var code = $@"
        class C
        {{
            private int {name};
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // === Parameters ===
    [TestMethod]
    [DataRow("tmp", true)]
    [DataRow("a", true)]
    [DataRow("data", true)]
    [DataRow("id", false)]
    [DataRow("ct", false)]
    public async Task Checks_Parameters(string name, bool expectDiagnostic)
    {
        var marked = expectDiagnostic ? "[|" + name + "|]" : name;
        var code = $@"
        class C
        {{
            void M(int {marked}) {{ }}
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // === Pattern variables ===
    [TestMethod]
    [DataRow("tmp")]
    [DataRow("a")]
    public async Task Flags_PatternVariables(string name)
    {
        var code = $@"
        class C
        {{
            void M(object input)
            {{
                if (input is int [|{name}|]) {{ }}
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("tmp")]
    [DataRow("a")]
    public async Task Flags_OutVariables(string name)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                TryGet(out int [|{name}|]);
            }}

            bool TryGet(out int result) {{ result = 0; return true; }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }


    [TestMethod]
    [DataRow("id")]
    [DataRow("ct")]
    public async Task DoesNotFlag_AllowedPatternVariables(string name)
    {
        var code = $@"
        class C
        {{
            void M(object input)
            {{
                if (input is int {name}) {{ }}
            }}
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("tmp", "val")]
    public async Task Flags_DeconstructionVariable_Local(string testItem1, string testItem2)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                var ([|{testItem1}|], [|{testItem2}|]) = (1, 2);
            }}
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("tmp", "val")]
    public async Task Flags_DeconstructionVariable_Foreach(string testItem1, string testItem2)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                foreach (var ([|{testItem1}|], [|{testItem2}|]) in new[] {{ (1, 2) }}) {{ }}
            }}
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    [DataRow("id", "ct")]
    [DataRow("ok", "db")]
    public async Task DoesNotFlag_AllowedDeconstructionVariables(string testItem1, string testItem2)
    {
        var code = $@"
        class C
        {{
            void M()
            {{
                var ({testItem1}, {testItem2}) = (1, 2);
            }}
        }}";
        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // EventArgs parameter 'e' should not be flagged, but other short names still are.
    [TestMethod]
    [DataRow("System.EventArgs", "e", false)]   // allowed convention
    [DataRow("System.EventArgs", "a", true)]    // still flagged
    [DataRow("int", "e", true)]                  // 'e' flagged if not EventArgs
    public async Task Checks_EventArgsParameterNames(string type, string name, bool expectDiagnostic)
    {
        var marked = expectDiagnostic ? "[|" + name + "|]" : name;
        var code = $@"
        using System;
        class UI
        {{
            void OnClick(object sender, {type} {marked}) {{ }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }


    // Mix of multiple locals + pattern variable + deconstruction, all in one method.
    // Expect 4 diagnostics: a, b1, tmp, val
    [TestMethod]
    [DataRow("a", "b1", "tmp", "val")]
    [DataRow("x", "y1", "foo", "bar")]
    public async Task Flags_Complex_Locals_Pattern_Deconstruction(string testItem1, string testItem2, string testItem3, string testItem4)
    {
        var code = $@"
        class C
        {{
            void M(object input)
            {{
                int [|{testItem1}|] = 0, [|{testItem2}|] = 1, count = 2;
                if (input is int [|{testItem3}|]) {{ }}
                var ([|{testItem4}|], good) = (1, 2);
            }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Multiple fields + parameters combined (no consts).
    // Each DataRow should yield 4 diagnostics (2 fields, 2 parameters).
    [TestMethod]
    [DataRow("tmp", "val", "a", "b1")]
    [DataRow("foo", "bar", "x", "y1")]
    [DataRow("obj", "data", "aa", "zz")]
    public async Task Flags_Complex_Fields_And_Parameters(string flag1, string flag2, string param1, string param2)
    {
        var code = $@"
        class C
        {{
            private int [|{flag1}|], id, [|{flag2}|];

            void M(int [|{param1}|], int id, int [|{param2}|]) {{ }}
        }}";

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code);
    }

    // Verifies the boolean suffix text appears in the diagnostic message.
    [TestMethod]
    public async Task MessageSuffix_Boolean()
    {
        var code = @"
        class C
        {
            void M()
            {
                bool b = false;
            }
        }";

        var expected = CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>
            .Diagnostic(WeakVariableNameAnalyzer.DiagnosticId)
            .WithSpan(6, 22, 6, 23) // line/column for "b"
            .WithArguments("b", " and since it is a boolean, prefer an 'is/has/can' prefix");

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code, expected);
    }


    [TestMethod]
    public async Task MessageSuffix_Collection()
    {
        var code = @"
        class C
        {
            void M()
            {
                System.Collections.Generic.List<int> data = default;
            }
        }";

        var expected = CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>
            .Diagnostic(WeakVariableNameAnalyzer.DiagnosticId)
            .WithSpan(6, 54, 6, 58) // line/column for "data"
            .WithArguments("data", " and since it is a collection, consider a plural name");

        await CSharpAnalyzerVerifier<WeakVariableNameAnalyzer>.VerifyAnalyzerAsync(code, expected);
    }

}
