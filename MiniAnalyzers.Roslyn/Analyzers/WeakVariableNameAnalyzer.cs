using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

/// <summary>
/// Flags short or non-descriptive names across multiple declaration contexts.
/// Targets:
/// - Local variables (including deconstruction and pattern variables)
/// - Fields (non-const)
/// - Parameters
///
/// Rules:
/// - Length rule: names of length ≤ 2 are flagged, except allow-list entries
/// - Token rule: names in a small weak-name set (e.g., "tmp", "data", "foo") are flagged
/// - For-loop counters: 'i', 'j', 'k' are allowed in the for-initializer only
/// - Discard identifier "_" is ignored
///
/// Diagnostic text is enriched with type-aware suggestions:
/// - For bool types: suggest 'is/has/can' prefix
/// - For arrays and IEnumerable&lt;T&gt;: suggest plural naming
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
        "Use descriptive names";

    // {1} is either empty or begins with a leading space like "
    //  and since it is a boolean, prefer an 'is/has/can' prefix"
    private static readonly LocalizableString MessageFormat =
        "Name '{0}' is short or non-descriptive{1}";

    private static readonly LocalizableString Description =
        "Very short or generic variable names hurt readability. Prefer a descriptive name " +
        "that reflects the variable's purpose (e.g., 'count', 'isReady', 'customerList').";

    // Names that are commonly placeholders or too generic for locals.
    // Case-insensitive to avoid accidental casing escapes (TMP, Tmp, etc.).
    private static readonly ImmutableHashSet<string> WeakNames =
     ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
         "tmp", "temp", "obj", "val", "data", "foo", "bar", "baz", "qux", "item", "elem", "thing", "stuff");

    // Short names that are widely accepted in code-bases and should not be flagged by the length rule.
    // Case-sensitive on purpose to keep naming style consistent.
    private static readonly ImmutableHashSet<string> AllowedShortNames =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "id", "ct", "ok", "db", "ip", "ui", "os");
    // A single site to carry the analyzed name, its type (if any), and the precise location to report.
    private readonly record struct NameCandidate(string Name, ITypeSymbol? Type, Location Location);
    // Compilation-scoped cache of well-known framework symbols needed by suggestions.
    private readonly record struct KnownTypes(
        INamedTypeSymbol? IEnumerableT,
        INamedTypeSymbol? EventArgs);

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
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
    /// Register once per-compilation, cache framework symbols, and pass them to all callbacks.
    /// This avoids resolving well-known types on every variable we analyze.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Resolve once. Null means the symbol could not be found, in which case collection hints will be skipped.
        context.RegisterCompilationStartAction(start =>
        {
            var known = new KnownTypes(
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1"),
                start.Compilation.GetTypeByMetadataName("System.EventArgs"));
            // Wrap your existing registrations so each callback receives 'known'
            start.RegisterSyntaxNodeAction(c => AnalyzeVariableDeclarator(c, known), SyntaxKind.VariableDeclarator);
            start.RegisterSymbolAction(c => AnalyzeParameter(c, known), SymbolKind.Parameter);
            start.RegisterSyntaxNodeAction(c => AnalyzeSingleDesignation(c, known), SyntaxKind.SingleVariableDesignation);
        });
    }
    // Dispatch variable declarators by context: locals vs fields.
    // Keeps each handler small and focused.
    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context, KnownTypes known)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Parent is not VariableDeclarationSyntax varDecl)
            return;

        var parent = varDecl.Parent;

        if (parent is LocalDeclarationStatementSyntax)
        {
            HandleLocalDeclarator(context, declarator, known);
            return;
        }

        if (parent is FieldDeclarationSyntax fieldDecl)
        {
            HandleFieldDeclarator(context, declarator, fieldDecl, known);
            return;
        }
    }

    // Local declarations: skip discards and classic for-counters, then analyze the declared symbol.
    private static void HandleLocalDeclarator(
    SyntaxNodeAnalysisContext context,
    VariableDeclaratorSyntax declarator,
    KnownTypes known)
    {
        var name = declarator.Identifier.ValueText;
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return;

        if (IsClassicForLoopCounter(declarator, name))
            return;

        var symbol = context.SemanticModel
            .GetDeclaredSymbol(declarator, context.CancellationToken) as ILocalSymbol;
        if (symbol is null)
            return;

        var candidate = new NameCandidate(name, symbol.Type, declarator.Identifier.GetLocation());
        EvaluateAndReportIfWeak(context, candidate, known);
    }

    // Field declarations: skip const fields to reduce noise on well-known constants, then analyze.
    private static void HandleFieldDeclarator(
        SyntaxNodeAnalysisContext context,
        VariableDeclaratorSyntax declarator,
        FieldDeclarationSyntax fieldDecl,
        KnownTypes known)
    {
        if (fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword))
            return;

        var name = declarator.Identifier.ValueText;
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return;

        var symbol = context.SemanticModel
            .GetDeclaredSymbol(declarator, context.CancellationToken) as IFieldSymbol;
        if (symbol is null)
            return;

        var candidate = new NameCandidate(name, symbol.Type, declarator.Identifier.GetLocation());
        EvaluateAndReportIfWeak(context, candidate, known);
    }

    // Treat arrays and any type implementing IEnumerable or IEnumerable<T> as collections.
    // Uses the cached IEnumerable<T> symbol for a robust and fast check.
    // Uses the cached IEnumerable<T> symbol for a robust and fast check.
    private static bool IsCollectionType(ITypeSymbol type, INamedTypeSymbol? ienumerableT)
    {
        if (type is IArrayTypeSymbol) return true;
        if (ienumerableT is null) return false;

        foreach (var i in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, ienumerableT))
                return true;
            if (i.SpecialType == SpecialType.System_Collections_IEnumerable)
                return true;
        }
        return false;
    }

    // Produces a single-sentence suffix to append to the message.
    // Keep the suffix fragment without a trailing period to preserve message style.
    private static string BuildSuggestionSuffix(ITypeSymbol? type, string name, KnownTypes known)
    {
        if (type is null)
            return string.Empty;

        if (type.SpecialType == SpecialType.System_Boolean)
            return " and since it is a boolean, prefer an 'is/has/can' prefix";

        if (IsCollectionType(type, known.IEnumerableT))
            return " and since it is a collection, consider a plural name";

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

    private static void AnalyzeParameter(SymbolAnalysisContext context, KnownTypes known)
    {
        var p = (IParameterSymbol)context.Symbol;

        // Noise reduction: conventional 'e' for EventArgs-typed parameters
        if (p.Name is "e" && DerivesFrom(p.Type, known.EventArgs))
            return;

        if (TryCreateCandidate(p, out var cand))
            EvaluateAndReportIfWeak(context, cand, known);
    }

    private static void AnalyzeSingleDesignation(SyntaxNodeAnalysisContext context, KnownTypes known)
    {
        var single = (SingleVariableDesignationSyntax)context.Node;
        var sym = context.SemanticModel.GetDeclaredSymbol(single, context.CancellationToken) as ILocalSymbol;
        if (TryCreateCandidate(sym, single.Identifier, out var cand))
            EvaluateAndReportIfWeak(context, cand, known);
    }

    // Central evaluation: apply naming rules, build type-aware suffix, and report.
    // This keeps all contexts consistent and maintains a single place to adjust behavior.
    private static void EvaluateAndReportIfWeak(SyntaxNodeAnalysisContext context, NameCandidate cand, KnownTypes known)
    {
        if (!ShouldFlagName(cand.Name))
            return;

        var suffix = BuildSuggestionSuffix(cand.Type, cand.Name, known);
        context.ReportDiagnostic(Diagnostic.Create(Rule, cand.Location, cand.Name, suffix));
    }

    private static void EvaluateAndReportIfWeak(SymbolAnalysisContext context, NameCandidate cand, KnownTypes known)
    {
        if (!ShouldFlagName(cand.Name))
            return;

        var suffix = BuildSuggestionSuffix(cand.Type, cand.Name, known);
        context.ReportDiagnostic(Diagnostic.Create(Rule, cand.Location, cand.Name, suffix));
    }

    // Decide whether a raw identifier is weak by length or token membership.
    // For the length rule we ignore exactly one leading underscore.
    // AllowedShortNames is checked on the final identifier we accept as "the name we want".
    private static bool ShouldFlagName(string name)
    {
        var trimmed = name.Length > 0 && name[0] == '_' ? name.Substring(1) : name;

        // Check allow-list against both original and trimmed forms.
        if (AllowedShortNames.Contains(name) || AllowedShortNames.Contains(trimmed))
            return false;

        if (trimmed.Length <= 2)
            return true;

        if (WeakNames.Contains(name))
            return true;

        return false;
    }
    private static bool TryCreateCandidate(ILocalSymbol? symbol, SyntaxToken id, out NameCandidate candidate)
    {
        candidate = default;
        if (symbol is null)
            return false;

        var name = id.ValueText;
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return false;

        candidate = new NameCandidate(name, symbol.Type, id.GetLocation());
        return true;
    }

    private static bool TryCreateCandidate(IParameterSymbol? symbol, out NameCandidate candidate)
    {
        candidate = default;
        if (symbol is null) return false;
        var name = symbol.Name;
        if (string.IsNullOrWhiteSpace(name) || name == "_") return false;

        var loc = symbol.Locations.FirstOrDefault();
        if (loc is null) return false;

        candidate = new NameCandidate(name, symbol.Type, loc);
        return true;
    }

    private static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (type is null || baseType is null) return false;
        for (var t = type as INamedTypeSymbol; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }
}
