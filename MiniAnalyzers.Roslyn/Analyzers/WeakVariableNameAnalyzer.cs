using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System;

namespace MiniAnalyzers.Roslyn.Analyzers;

/// <summary>
/// Flags local variables that have weak, very short, or non-descriptive names
/// (e.g., <c>a</c>, <c>b1</c>, <c>tmp</c>).
///
/// Scope (first iteration):
/// - Only local variable declarations (not fields, parameters, or deconstruction).
/// - Skips typical for-loop counters 'i', 'j', 'k' when declared in the loop initializer.
/// - Keeps rules simple: names with length ≤ 2 or exactly "tmp".
/// 
/// Follow-ups planned:
/// - Consider "obj", "val", "data", "flag", etc.
/// - Consider type-aware hints (bool → "isX"/"hasX", collections → plural).
/// - Handle deconstruction and pattern variables.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WeakVariableNameAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Keep the ID in sync with numbering.
    /// </summary>
    public const string DiagnosticId = "MNA0004";

    private const string Category = "Naming";

    private static readonly LocalizableString Title =
        "Use descriptive variable names";

    // {1} is either empty or begins with a leading space like "
    //  and since it is a boolean, prefer an 'is/has/can' prefix"
    private static readonly LocalizableString MessageFormat =
        "Local variable '{0}' has a short or non-descriptive name{1}";

    private static readonly LocalizableString Description =
        "Very short or generic variable names hurt readability. Prefer a descriptive name " +
        "that reflects the variable's purpose (e.g., 'count', 'isReady', 'customerList').";

    // Very small, conservative list to start with.
    private static readonly HashSet<string> WeakNames =
        new(StringComparer.OrdinalIgnoreCase) { "tmp", "temp", "obj", "val", "data", "foo", "bar", "baz", "qux", "item", "elem", "thing", "stuff" };

    // Short names that are widely accepted in locals and should not be flagged.
    private static readonly HashSet<string> AllowedShortNames =
        new(StringComparer.Ordinal)
        {
        "id",    // common abbreviation for identifier
        "ct",    // CancellationToken
        "ok",    // common flag result
        "db",    // database
        "ip",    // internet protocol
        "ui",    // user interface
        "os"     // operating system
        };

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        // Match previous analyzers:
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Start small: just variable declarators inside local declarations.
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);

        // Parameters: use a symbol action for direct access to IParameterSymbol.
        context.RegisterSymbolAction(AnalyzeParameter, SymbolKind.Parameter);

        // Pattern variables and deconstructions use variable designations.
        context.RegisterSyntaxNodeAction(AnalyzeSingleDesignation, SyntaxKind.SingleVariableDesignation);
        context.RegisterSyntaxNodeAction(AnalyzeParenthesizedDesignation, SyntaxKind.ParenthesizedVariableDesignation);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Parent is not VariableDeclarationSyntax varDecl)
            return;

        var parent = varDecl.Parent;

        // Locals: same as before
        if (parent is LocalDeclarationStatementSyntax)
        {
            var name = declarator.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(name) || name == "_")
                return;

            if (IsClassicForLoopCounter(declarator, name))
                return;

            var symbol = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) as ILocalSymbol;
            if (symbol is null) return;

            EvaluateAndReportIfWeak(context, name, symbol.Type, declarator.Identifier.GetLocation());
            return;
        }

        // Fields: now supported, but skip 'const' fields to reduce noise on e.g. "PI"
        if (parent is FieldDeclarationSyntax fieldDecl)
        {
            // Skip constants
            if (fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword))
                return;

            var name = declarator.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(name) || name == "_")
                return;

            var symbol = context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) as IFieldSymbol;
            if (symbol is null) return;

            EvaluateAndReportIfWeak(context, name, symbol.Type, declarator.Identifier.GetLocation());
        }
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        // Use the interfaces already available on the type, and compare by metadata name.
        return type.AllInterfaces.Any(i =>
            i.SpecialType == SpecialType.System_Collections_IEnumerable ||
            (i.OriginalDefinition is INamedTypeSymbol named &&
             named.ConstructedFrom?.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>"));
    }


    private static string BuildSuggestionSuffix(ITypeSymbol? type, string name)
    {
        if (type is null)
            return string.Empty;

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            // Keep it a single sentence; no trailing period.
            return " and since it is a boolean, prefer an 'is/has/can' prefix";
        }

        if (IsCollectionType(type))
        {
            // Do not attempt pluralization; just nudge.
            return " and since it is a collection, consider a plural name";
        }

        return string.Empty;
    }

    private static bool IsClassicForLoopCounter(VariableDeclaratorSyntax declarator, string name)
    {
        // Allow i/j/k in for-initializer declarations:
        // for (int i = 0; i < n; i++) { ... }
        if (!(name is "i" or "j" or "k"))
            return false;

        if (declarator.Parent is not VariableDeclarationSyntax varDecl)
            return false;

        // The variable declaration must belong to a ForStatement's Declaration.
        if (varDecl.Parent?.Parent is ForStatementSyntax forStmt)
        {
            return forStmt.Declaration == varDecl;
        }

        return false;
    }

    private static void AnalyzeParameter(SymbolAnalysisContext context)
    {
        var parameter = (IParameterSymbol)context.Symbol;
        var name = parameter.Name;
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return;

        // Typical framework and your allow-list still apply
        EvaluateAndReportIfWeak(context, name, parameter.Type, parameter.Locations.FirstOrDefault());
    }

    private static void AnalyzeSingleDesignation(SyntaxNodeAnalysisContext context)
    {
        // Covers: if (x is int n), switch patterns, out var n, and deconstruct parts (x) inside (x, y).
        var single = (SingleVariableDesignationSyntax)context.Node;
        var name = single.Identifier.ValueText;
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return;

        // Get the declared symbol for the designation
        var symbol = context.SemanticModel.GetDeclaredSymbol(single, context.CancellationToken) as ILocalSymbol;
        if (symbol is null) return;

        EvaluateAndReportIfWeak(context, name, symbol.Type, single.Identifier.GetLocation());
    }

    private static void AnalyzeParenthesizedDesignation(SyntaxNodeAnalysisContext context)
    {
        // Covers: var (x, y) = Get(), foreach (var (x, y) in ...)
        var paren = (ParenthesizedVariableDesignationSyntax)context.Node;

        foreach (var single in paren.Variables.OfType<SingleVariableDesignationSyntax>())
        {
            var name = single.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(name) || name == "_")
                continue;

            var symbol = context.SemanticModel.GetDeclaredSymbol(single, context.CancellationToken) as ILocalSymbol;
            if (symbol is null) continue;

            EvaluateAndReportIfWeak(context, name, symbol.Type, single.Identifier.GetLocation());
        }
    }

    private static void EvaluateAndReportIfWeak(
    SyntaxNodeAnalysisContext context,
    string name,
    ITypeSymbol? type,
    Location? location)
    {
        if (!ShouldFlagName(name))
            return;

        var suffix = BuildSuggestionSuffix(type, name);
        if (location is null) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name, suffix));
    }

    private static void EvaluateAndReportIfWeak(
        SymbolAnalysisContext context,
        string name,
        ITypeSymbol? type,
        Location? location)
    {
        if (!ShouldFlagName(name))
            return;

        var suffix = BuildSuggestionSuffix(type, name);
        if (location is null) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name, suffix));
    }

    private static bool ShouldFlagName(string name)
    {
        // leading/trailing whitespace is already filtered by callers
        // Length rule (<= 2) with allow-list
        if (name.Length <= 2 && !AllowedShortNames.Contains(name))
            return true;

        // Token rule
        if (WeakNames.Contains(name))
            return true;

        return false;
    }
}
