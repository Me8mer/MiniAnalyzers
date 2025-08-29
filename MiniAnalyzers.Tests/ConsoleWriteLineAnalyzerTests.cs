using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiniAnalyzers.Roslyn.Analyzers;
using MiniAnalyzers.Tests.Verifiers;

namespace MiniAnalyzers.Tests;

[TestClass]
public sealed class ConsoleWriteLineAnalyzerTests
{
    [TestMethod]
    public async Task Flags_Basic_ConsoleWriteLine()
    {
        var code = @"
using System;
class C
{
    void M()
    {
        Console.{|MNA0003:WriteLine|}(""hello"");

    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_FullyQualified_SystemConsoleWriteLine()
    {
        var code = @"
class C
{
    void M()
    {
        System.Console.{|MNA0003:WriteLine|}();

    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_WithTypeAlias_ToSystemConsole()
    {
        var code = @"
using ConsoleAlias = System.Console;
class C
{
    void M()
    {
        ConsoleAlias.{|MNA0003:WriteLine|}(""x"");
    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_CustomConsoleClass()
    {
        var code = @"
class Console { public static void WriteLine() { } }
class C
{
    void M()
    {
        Console.WriteLine(); // user-defined, should not be flagged
    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task DoesNotFlag_DebugWriteLine()
    {
        var code = @"
class C
{
    void M()
    {
        System.Diagnostics.Debug.WriteLine(""debug"");
    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_Basic_ConsoleWrite()
    {
        var code = @"
using System;
class C
{
    void M()
    {
        Console.{|MNA0003:Write|}(""hello"");
    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_FullyQualified_SystemConsoleWrite()
    {
        var code = @"
class C
{
    void M()
    {
        System.Console.{|MNA0003:Write|}(""x"");

    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task Flags_StaticUsing_Write_And_WriteLine()
    {
        var code = @"
using static System.Console;
class C
{
    void M()
    {
        {|MNA0003:Write|}(""a"");
        {|MNA0003:WriteLine|}(""b"");
    }
}";
        await CSharpAnalyzerVerifier<ConsoleWriteLineAnalyzer>.VerifyAnalyzerAsync(code);
    }
}
