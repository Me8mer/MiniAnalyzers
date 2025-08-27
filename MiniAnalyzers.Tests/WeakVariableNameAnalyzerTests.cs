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
    [DataRow("count")]
    [DataRow("isReady")]
    [DataRow("customers")]
    [DataRow("_")]
    [DataRow("id")]
    [DataRow("ct")]
    [DataRow("ok")]
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
    [DataRow("b")]      // length <= 2
    [DataRow("tmp")]    // weak token
    [DataRow("data")]   // weak token
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

}
