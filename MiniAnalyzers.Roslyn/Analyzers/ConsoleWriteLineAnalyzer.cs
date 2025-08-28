using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

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

    private const string Category = "Maintainability";

    private static readonly LocalizableString Title =
        "Avoid Console.Write/WriteLine in production code";

    private static readonly LocalizableString MessageFormat =
        "Replace Console.Write/WriteLine with a logging framework";

    private static readonly LocalizableString Description =
        "Console I/O is brittle for diagnostics. Use a structured logging framework that supports levels, sinks, and configuration.";

    private const string RecommendationText =
    "Replace Console.Write/WriteLine with a logging API. Prefer ILogger or Debug.WriteLine for diagnostics.";
   
    private static readonly ImmutableDictionary<string, string?> RecommendationProps =
    ImmutableDictionary<string, string?>.Empty.Add("Suggestion", RecommendationText);


    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

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
        var op = (IInvocationOperation)context.Operation;

        // Quick check. We only care about methods named WriteLine and Write.
        if (op.TargetMethod.Name != "WriteLine" && op.TargetMethod.Name != "Write")
            return;

        // Resolve System.Console and compare the containing type precisely.
        var consoleType = context.Compilation.GetTypeByMetadataName("System.Console");
        if (consoleType is null)
            return;

        if (!SymbolEqualityComparer.Default.Equals(op.TargetMethod.ContainingType, consoleType))
            return;

        // Report at the identifier 'WriteLine' when possible for a precise highlight.
        var location = GetNameLocation(op) ?? op.Syntax.GetLocation();

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, properties: RecommendationProps));
    }

    private static Location? GetNameLocation(IInvocationOperation op)
    {
        if (op.Syntax is InvocationExpressionSyntax inv)
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
}
