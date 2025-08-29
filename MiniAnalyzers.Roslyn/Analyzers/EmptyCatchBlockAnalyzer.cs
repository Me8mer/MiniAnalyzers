using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using MiniAnalyzers.Roslyn.Infrastructure.Common;


namespace MiniAnalyzers.Roslyn.Analyzers;

/// <summary>
/// Flags empty catch blocks that swallow exceptions without handling.
/// First pass: report when the catch block contains zero statements.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCatchBlockAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Keep IDs stable and consistent with the project numbering.</summary>
    public const string DiagnosticId = "MNA0002";
    private const string Category = "Reliability";

    private static readonly LocalizableString Title =
        "Do not use empty 'catch' blocks";

    private static readonly LocalizableString MessageFormat =
        "Empty catch block. Handle, log, or rethrow the exception.";

    private static readonly LocalizableString Description =
       "Empty catch blocks hide failures and complicate debugging. Handle the exception, log it, or rethrow.";

    private const string RecommendationText =
        "Do not leave catch blocks empty. Either rethrow with throw, log the exception, or handle it explicitly.";

    private static readonly ImmutableDictionary<string, string?> RecommendationProps =
    ImmutableDictionary<string, string?>.Empty.Add("Suggestion", RecommendationText);
    // "System.Threading.Tasks.TaskCanceledException"
    private static string GetFullName(ITypeSymbol type) =>
    type.ToDisplayString();

    private readonly struct KnownTypes
    {
        public KnownTypes(INamedTypeSymbol? operationCanceledException) =>
            OperationCanceledException = operationCanceledException;

        public INamedTypeSymbol? OperationCanceledException { get; }
    }

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);
    /// <summary>
    /// Called once when the analyzer is initialized for a given compilation.
    /// <para>
    /// This method configures analysis options (ignoring generated code,
    /// enabling concurrency) and registers the callbacks we want Roslyn
    /// to invoke, in this case, for every <c>catch</c> clause.
    /// </para>
    /// </summary>
    /// <param name="context">
    /// The <see cref="AnalysisContext"/> used to register analysis actions.
    /// </param>
    public override void Initialize(AnalysisContext context)
    {
        // Do not analyze generated code and allow concurrency.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startCtx =>
        {
            var oce = startCtx.Compilation.GetTypeByMetadataName("System.OperationCanceledException");
            var known = new KnownTypes(oce);

            startCtx.RegisterSyntaxNodeAction(ctx => AnalyzeCatchClause(ctx, known), SyntaxKind.CatchClause);
        });
    }

    /// <summary>
    /// Reports a diagnostic when a catch block has no executable statements.
    /// We report on the 'catch' keyword to make the highlight obvious.
    /// </summary>
    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context, KnownTypes known)
    {
        var clause = (CatchClauseSyntax)context.Node;

        var options = AnalyzerOptionExtensions.GetEmptyCatchOptions(context);

        var block = clause.Block;
        if (block is null)
            return;

        if (!IsEffectivelyEmpty(block, options.TreatEmptyStatementAsEmpty))
            return;

        if (clause.Declaration?.Type is TypeSyntax typeSyntax)
        {
            var caughtType = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type;

            if (options.IgnoreCancellation && IsCancellationExceptionType(caughtType, known))
                return;

            if (options.AllowedExceptionTypes is { Count: > 0 } set &&
                caughtType is not null &&
                set.Contains(GetFullName(caughtType)))
            {
                return;
            }
        }

        var location = clause.CatchKeyword.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, properties: RecommendationProps));
    }

    //helper to decide “effective emptiness”.
    private static bool IsEffectivelyEmpty(BlockSyntax block, bool treatEmptyStatementAsEmpty)
    {
        if (block.Statements.Count == 0)
            return true;

        if (!treatEmptyStatementAsEmpty)
            return false;

        foreach (var statement in block.Statements)
        {
            if (statement is not EmptyStatementSyntax)
                return false;
        }
        return true;
    }

    // Simple inheritance walk used by the cancellation check.
    private static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var t = type as INamedTypeSymbol; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }

    // True if the caught type is OperationCanceledException or derives from it.
    // This also covers TaskCanceledException which derives from OperationCanceledException.
    private static bool IsCancellationExceptionType(ITypeSymbol? caughtType, in KnownTypes known)
    {
        if (caughtType is null || known.OperationCanceledException is null)
            return false;

        var oce = known.OperationCanceledException;
        return SymbolEqualityComparer.Default.Equals(caughtType, oce) || DerivesFrom(caughtType, oce);
    }

}
