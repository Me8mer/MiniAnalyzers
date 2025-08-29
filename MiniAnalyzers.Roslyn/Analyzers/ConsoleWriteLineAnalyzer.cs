using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using MiniAnalyzers.Roslyn.Infrastructure.Common;
using MiniAnalyzers.Roslyn.Infrastructure.Options.ConsoleWrite;
namespace MiniAnalyzers.Roslyn.Analyzers;

/// <summary>
/// Flags calls to <c>System.Console.WriteLine</c>.
/// Rationale: Console I/O is not suitable for production diagnostics.
/// Prefer a structured logging framework.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConsoleWriteLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Keep IDs stable and consistent with numbering.</summary>
    public const string DiagnosticId = "MNA0003";

    /// <summary>Keep IDs stable and consistent with numbering.</summary>
    public const string DiagnosticIdA = "MNA0003A";

    private const string Category = "Maintainability";

    private static readonly LocalizableString Title =
        "Avoid Console.Write/WriteLine in production code";
    private static readonly LocalizableString TitleA =
        "Console message should start with the required prefix";

    private static readonly LocalizableString MessageFormat =
        "Replace Console.Write/WriteLine with a logging framework";

    private static readonly LocalizableString MessageFormatA =
       "Console message should start with '{0}'";

    private static readonly LocalizableString Description =
        "Console I/O is brittle for diagnostics. Use a structured logging framework that supports levels, sinks, and configuration.";

    private static readonly LocalizableString DescriptionA =
        "Project policy requires a prefix for Console output to aid log filtering.";

    private const string RecommendationText =
    "Replace Console.Write/WriteLine with a logging API. Prefer ILogger or Debug.WriteLine for diagnostics.";

    // Suggestion for the prefix rule – includes the required prefix to be actionable.
    private const string RecommendationTextPrefixTemplate =
        "Prepend the required prefix '{0}' to the Console message, or use a logging API that adds context automatically (e.g., ILogger).";

    private static readonly ImmutableDictionary<string, string?> RecommendationProps =
    ImmutableDictionary<string, string?>.Empty.Add("Suggestion", RecommendationText);

    // Builds a per-diagnostic properties bag so the UI can show a concrete suggestion.
    private static ImmutableDictionary<string, string?> BuildPrefixSuggestionProps(string requiredPrefix)
    {
        var formatted = string.Format(RecommendationTextPrefixTemplate, requiredPrefix);
        return ImmutableDictionary<string, string?>.Empty
            .Add("Suggestion", formatted)
            .Add("RequiredPrefix", requiredPrefix);
    }

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    private static readonly DiagnosticDescriptor RuleMissingPrefix = new(
        id: DiagnosticIdA,
        title:TitleA,
        messageFormat: MessageFormatA,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: DescriptionA);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
       ImmutableArray.Create(Rule, RuleMissingPrefix);

    /// <summary>
    /// Registers operation analysis for invocations of <c>System.Console.Write</c>/<c>WriteLine</c>.
    /// Skips generated code and enables concurrent execution.
    /// </summary>
    /// <param name="context">Analyzer registration context.</param>
    public override void Initialize(AnalysisContext context)
    {
        // Do not analyze generated code and allow concurrency.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // We only need invocation callbacks.
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var operation = (IInvocationOperation)context.Operation;

        // Quick check. We only care about methods named WriteLine and Write.
        if (operation.TargetMethod.Name != "WriteLine" && operation.TargetMethod.Name != "Write")
            return;

        // Resolve System.Console and compare the containing type precisely.
        var consoleType = context.Compilation.GetTypeByMetadataName("System.Console");
        if (consoleType is null)
            return;

        if (!SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, consoleType))
            return;

        var options = context.GetConsoleWriteOptions();

        if (options.AllowInTopLevel && IsInTopLevel(operation.Syntax))
            return;

        if (options.AllowInTests && IsInTestContext(operation))
            return;

        if (TryReportMissingPrefix(context, operation, options))
            return;

        // Report at the identifier 'WriteLine' when possible for a precise highlight.
        var location = GetNameLocation(operation) ?? operation.Syntax.GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, properties: RecommendationProps));
    }
    private static bool IsInTopLevel(SyntaxNode node) =>
    node.AncestorsAndSelf().Any(n => n is GlobalStatementSyntax);

    // Minimal heuristic for tests: attributes like [TestMethod], [Fact], [Theory], [Test], class fixture markers,
    // or a file path that contains "Tests".
    private static bool IsInTestContext(IInvocationOperation operation)
    {
        // file path hint
        var path = operation.Syntax.SyntaxTree?.FilePath;
        if (!string.IsNullOrEmpty(path) && path!.IndexOf("Tests", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // attribute hint on method or containing type
        var method = operation.SemanticModel?.GetEnclosingSymbol(operation.Syntax.SpanStart) as IMethodSymbol;
        if (method is null)
            return false;

        static bool HasTestAttribute(ImmutableArray<AttributeData> attrs)
        {
            foreach (var a in attrs)
            {
                var name = a.AttributeClass?.Name;
                if (name is null) continue;
                if (name is "TestMethodAttribute" or "TestClassAttribute" // MSTest
                    or "FactAttribute" or "TheoryAttribute"              // xUnit
                    or "TestAttribute" or "TestCaseAttribute"            // NUnit
                    or "TestFixtureAttribute")
                    return true;
            }
            return false;
        }

        if (HasTestAttribute(method.GetAttributes()))
            return true;

        var containing = method.ContainingType;
        while (containing is not null)
        {
            if (HasTestAttribute(containing.GetAttributes()))
                return true;
            containing = containing.ContainingType;
        }
        return false;
    }

    private static Location? GetNameLocation(IInvocationOperation operation)
    {
        if (operation.Syntax is InvocationExpressionSyntax inv)
        {
            // Console.WriteLine(...)
            if (inv.Expression is MemberAccessExpressionSyntax member)
                return member.Name.GetLocation();

            // using static System.Console; WriteLine(...)
            if (inv.Expression is IdentifierNameSyntax id)
                return id.GetLocation();
        }
        return null;
    }

    private static bool TryReportMissingPrefix(OperationAnalysisContext context, IInvocationOperation invocation, ConsoleWriteOptions options)
    {
        if (string.IsNullOrEmpty(options.RequiredPrefix))
            return false;

        if (HasRequiredPrefix(invocation, options))
            return false;

        // Prefer the actual message argument location if we can find it.
        Location reportLocation;
        if (TryGetMessageArgument(invocation, out var messageValue))
        {
            reportLocation = messageValue.Syntax.GetLocation();
        }
        else
        {
            reportLocation = GetNameLocation(invocation) ?? invocation.Syntax.GetLocation();
        }

        context.ReportDiagnostic(Diagnostic.Create(
            RuleMissingPrefix,
            reportLocation,
            properties: BuildPrefixSuggestionProps(options.RequiredPrefix),
            options.RequiredPrefix));

        return true;
    }


    private static bool HasRequiredPrefix(IInvocationOperation operation, ConsoleWriteOptions options)
    {
        if (!TryGetMessageArgument(operation, out var messageValue))
            return true; // No string-like message available. Skip to avoid noise.

        if (!TryGetLeadingText(messageValue, out var leading))
            return true; // Not a compile-time string. Skip to avoid noise.

        var comparison = options.RequiredPrefixIgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return leading.StartsWith(options.RequiredPrefix, comparison);
    }

    private static bool TryGetLeadingText(IOperation valueOperation, out string text)
    {
        // Case 1: "literal"
        if (valueOperation is ILiteralOperation literal &&
            literal.ConstantValue.HasValue &&
            literal.ConstantValue.Value is string literalText)
        {
            text = literalText;
            return true;
        }

        // Case 2: $"text{expr}" – use only the first text chunk
        if (valueOperation is IInterpolatedStringOperation interpolated &&
            interpolated.Parts.Length > 0 &&
            interpolated.Parts[0] is IInterpolatedStringTextOperation textOp &&
            textOp.Text is ILiteralOperation textLiteral &&
            textLiteral.ConstantValue.HasValue &&
            textLiteral.ConstantValue.Value is string firstChunk)
        {
            text = firstChunk;
            return true;
        }

        // Not a compile-time string with a leading text segment – skip to avoid noise
        text = string.Empty;
        return false;
    }

    private static bool TryGetMessageArgument(IInvocationOperation operation, out IOperation messageArg)
    {
        foreach (var argument in operation.Arguments)
        {
            var value = argument.Value;
            // string literal, string variable, or interpolated string
            if (value.Type?.SpecialType == SpecialType.System_String || value is IInterpolatedStringOperation)
            {
                messageArg = value;
                return true;
            }
        }

        messageArg = null!;
        return false;
    }

}
