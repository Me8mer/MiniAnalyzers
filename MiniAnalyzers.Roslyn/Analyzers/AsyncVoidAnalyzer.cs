using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;
using System; 

namespace MiniAnalyzers.Roslyn.Analyzers;

/// <summary>
/// Analyzer that detects usage of <c>async void</c> methods and local functions.
/// 
/// <para>
/// Why this matters:
/// <list type="bullet">
///   <item>Exceptions in <c>async void</c> bypass normal <c>try/catch</c> flow and crash the process.</item>
///   <item><c>async void</c> cannot be awaited, which makes them hard to test or compose.</item>
///   <item>The recommended practice is to return <see cref="System.Threading.Tasks.Task"/> or <see cref="System.Threading.Tasks.Task{TResult}"/>.</item>
/// </list>
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Unique identifier for this diagnostic rule.
    /// IDs should remain stable, since users and tools rely on them.
    /// </summary>
    public const string DiagnosticId = "MNA0001";

    /// <summary>Title of the diagnostic, shown in IDE tooltips.</summary>
    private static readonly LocalizableString Title =
        "Avoid 'async void' methods";

    /// <summary>
    /// Message format string used in the warning itself.
    /// <para>Example: <c>Method 'DoWork' is 'async void'. Return Task or Task&lt;T&gt; instead.</c></para>
    /// </summary>
    private static readonly LocalizableString MessageFormat =
        "Method '{0}' is 'async void'. Return Task or Task<T> instead.";

    /// <summary>Longer description explaining why this is a problem.</summary>
    private static readonly LocalizableString Description =
        "Async void methods cannot be awaited and exceptions bypass normal handling. " +
        "Prefer Task returning async methods for reliability and testability.";

    /// <summary>Category groups related rules. Often 'Reliability', 'Design', etc.</summary>
    private const string Category = "Reliability";

    /// <summary>
    /// The <see cref="DiagnosticDescriptor"/> holds all metadata about the rule:
    /// ID, title, message, category, severity, and description.
    /// </summary>
    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
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
    /// Called once at analyzer load. 
    /// We register which syntax nodes we are interested in (methods, local functions).
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        // Do not analyze auto-generated code (like .Designer.cs).
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // Allow multiple threads for performance.
        context.EnableConcurrentExecution();

        // Register callbacks for method and local function declarations.
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
        context.RegisterOperationAction(AnalyzeAnonymousFunctionConversion, OperationKind.Conversion);
        context.RegisterOperationAction(AnalyzeAnonymousFunctionDelegateCreation, OperationKind.DelegateCreation);
    }

    /// <summary>
    /// Analyzes ordinary method declarations for <c>async void</c>.
    /// </summary>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var node = (MethodDeclarationSyntax)context.Node;

        // Quick syntax check: only async + void methods matter.
        if (!node.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            return;

        if (node.ReturnType is not PredefinedTypeSyntax pts ||
            !pts.Keyword.IsKind(SyntaxKind.VoidKeyword))
            return;

        // Retrieve symbol for semantic information (like method kind).
        var symbol = context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) as IMethodSymbol;
        if (symbol is null)
            return;

        // Skip constructors, operators, etc.
        if (symbol.MethodKind != MethodKind.Ordinary)
            return;

        // Skip overrides or explicit interface implementations
        // (we avoid noise where the signature is forced).
        if (symbol.IsOverride || symbol.ExplicitInterfaceImplementations.Length > 0)
            return;


        // Resolve System.EventArgs once per callback
        var eventArgsSymbol = context.SemanticModel.Compilation
            .GetTypeByMetadataName("System.EventArgs");

        // Skip common event handler pattern to reduce noise
        if (LooksLikeEventHandler(symbol, eventArgsSymbol))
            return;

        // Report the diagnostic at the 'void' keyword location.
        var reportLocation = node.ReturnType.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation, symbol.Name));
    }

    /// <summary>
    /// Analyzes local functions for <c>async void</c>.
    /// </summary>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var node = (LocalFunctionStatementSyntax)context.Node;

        if (!node.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            return;

        if (node.ReturnType is not PredefinedTypeSyntax pts ||
            !pts.Keyword.IsKind(SyntaxKind.VoidKeyword))
            return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) as IMethodSymbol;
        if (symbol is null)
            return;

        // Local functions have MethodKind.LocalFunction.
        if (symbol.MethodKind != MethodKind.LocalFunction)
            return;

        var reportLocation = node.ReturnType.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(Rule, reportLocation, symbol.Name));
    }

    /// <summary>
    /// Detect async lambdas converted to void-returning delegates via implicit/explicit conversion,
    /// e.g. 'Action a = async () => ...;' or passing async lambda to a void delegate parameter.
    /// </summary>
    private static void AnalyzeAnonymousFunctionConversion(OperationAnalysisContext context)
    {
        var conv = (IConversionOperation)context.Operation;

        // We only care when converting *to a delegate* type that returns void => async void scenario.
        if (conv.Type is not INamedTypeSymbol delegateType)
            return;

        var invoke = delegateType.DelegateInvokeMethod;
        if (invoke is null || !invoke.ReturnsVoid)
            return;

        // Operand must be an anonymous function (lambda or anonymous method)
        if (conv.Operand is not IAnonymousFunctionOperation anon)
            return;

        // Check for 'async' via syntax (reliable for anonymous functions)
        if (anon.Syntax is not AnonymousFunctionExpressionSyntax lambdaSyntax ||
            lambdaSyntax.AsyncKeyword.RawKind == 0)
            return;

        // Skip typical event-handler-shaped delegates: (object, EventArgs-or-derived)
        var eventArgsSymbol = context.Compilation.GetTypeByMetadataName("System.EventArgs");
        if (LooksLikeEventHandlerDelegate(delegateType, eventArgsSymbol))
            return;

        var name = anon.Symbol?.Name ?? "(anonymous)";
        context.ReportDiagnostic(Diagnostic.Create(Rule, anon.Syntax.GetLocation(), name));
    }

    /// <summary>
    /// Detect async lambdas wrapped in an explicit delegate creation,
    /// e.g. 'Action a = new Action(async () => ...);'
    /// </summary>
    private static void AnalyzeAnonymousFunctionDelegateCreation(OperationAnalysisContext context)
    {
        var del = (IDelegateCreationOperation)context.Operation;

        if (del.Type is not INamedTypeSymbol delegateType)
            return;

        var invoke = delegateType.DelegateInvokeMethod;
        if (invoke is null || !invoke.ReturnsVoid)
            return;

        if (del.Target is not IAnonymousFunctionOperation anon)
            return;

        if (anon.Syntax is not AnonymousFunctionExpressionSyntax lambdaSyntax ||
            lambdaSyntax.AsyncKeyword.RawKind == 0)
            return;

        var eventArgsSymbol = context.Compilation.GetTypeByMetadataName("System.EventArgs");
        if (LooksLikeEventHandlerDelegate(delegateType, eventArgsSymbol))
            return;

        var name = anon.Symbol?.Name ?? "(anonymous)";
        context.ReportDiagnostic(Diagnostic.Create(Rule, anon.Syntax.GetLocation(), name));
    }




    private static bool LooksLikeEventHandler(IMethodSymbol method, INamedTypeSymbol? eventArgsSymbol)
    {
        if (method.Parameters.Length != 2)
            return false;

        var sender = method.Parameters[0];
        var args = method.Parameters[1];

        bool senderOk = sender.Type.SpecialType == SpecialType.System_Object;

        bool derivesFromEventArgs = eventArgsSymbol is not null &&
                                    DerivesFrom(args.Type, eventArgsSymbol);

        bool looksLikeEventArgsByName = args.Type.Name.EndsWith("EventArgs", StringComparison.Ordinal);

        return senderOk && (derivesFromEventArgs || looksLikeEventArgsByName);
    }

    private static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }
        return false;
    }
    private static bool LooksLikeEventHandlerDelegate(INamedTypeSymbol? delegateType, INamedTypeSymbol? eventArgsSymbol)
    {
        if (delegateType is null)
            return false;

        var invoke = delegateType.DelegateInvokeMethod;
        if (invoke is null)
            return false;

        var parameters = invoke.Parameters;
        if (parameters.Length != 2)
            return false;

        var senderType = parameters[0].Type;
        var argsType = parameters[1].Type;

        bool senderOk = senderType.SpecialType == SpecialType.System_Object;

        bool derivesFromEventArgs = eventArgsSymbol is not null &&
                                    DerivesFrom(argsType, eventArgsSymbol);

        bool looksLikeEventArgsByName = argsType.Name.EndsWith("EventArgs", StringComparison.Ordinal);

        return senderOk && (derivesFromEventArgs || looksLikeEventArgsByName);
    }


}
