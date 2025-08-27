using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MiniAnalyzers.CodeFixes;

/// <summary>
/// Code fix for <c>MNA0001</c> that offers two safe transformations:
/// <list type="number">
///   <item>
///     Change the return type of an <c>async void</c> method or local function to
///     <c>System.Threading.Tasks.Task</c>.
///   </item>
///   <item>
///     For an <c>async</c> lambda assigned to a void returning delegate with zero parameters
///     (for example <c>Action</c>), change the declared delegate type to
///     <c>System.Func&lt;System.Threading.Tasks.Task&gt;</c>.
///   </item>
/// </list>
/// The provider keeps edits local and conservative. It does not add using directives
/// or rewrite call sites. It uses fully qualified type names to avoid extra edits.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFixProvider))]
[Shared]
public sealed class AsyncVoidCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// The set of diagnostic IDs this code fix can address.
    /// Currently handles only <c>MNA0001</c>.
    /// </summary>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MiniAnalyzers.Roslyn.Analyzers.AsyncVoidAnalyzer.DiagnosticId);

    /// <summary>
    /// Uses the standard batch fixer for Fix All.
    /// </summary>
    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    /// <summary>
    /// Registers code actions for the span reported by the analyzer.
    /// Chooses one of three contexts:
    /// method declaration, local function declaration, or an async lambda
    /// assigned to a void returning delegate.
    /// </summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var cancellation = context.CancellationToken;

        var root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var span = diagnostic.Location.SourceSpan;

        // Find a node that sits at or above the diagnostic location.
        var token = root.FindToken(span.Start);
        var node = token.Parent;
        if (node is null)
            return;

        // Case 1: method declaration. Change 'void' to 'System.Threading.Tasks.Task'.
        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change return type to Task",
                    createChangedDocument: ct => ChangeMethodReturnTypeToTaskAsync(document, method, ct),
                    equivalenceKey: "MNA0001_Method_ToTask"),
                diagnostic);
            return;
        }

        // Case 2: local function. Change 'void' to 'System.Threading.Tasks.Task'.
        var localFunc = node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>();
        if (localFunc is not null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Change return type to Task",
                    createChangedDocument: ct => ChangeLocalFunctionReturnTypeToTaskAsync(document, localFunc, ct),
                    equivalenceKey: "MNA0001_LocalFunc_ToTask"),
                diagnostic);
            return;
        }

        // Case 3: async lambda assigned to a void delegate with zero parameters (for example Action).
        // We only offer a fix when the declaration uses an explicit delegate type
        // and declares a single variable, to keep the edit simple.
        var lambda = node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>();
        if (lambda is not null)
        {
            var variableDecl = lambda.FirstAncestorOrSelf<VariableDeclarationSyntax>();
            if (variableDecl is not null)
            {
                var isVar =
                    variableDecl.Type is IdentifierNameSyntax id &&
                    id.Identifier.Text == "var";

                if (!isVar && variableDecl.Variables.Count == 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Change delegate type to Func<Task>",
                            createChangedDocument: ct => ChangeDelegateTypeToFuncTaskAsync(document, variableDecl, ct),
                            equivalenceKey: "MNA0001_Lambda_ToFuncTask"),
                        diagnostic);
                }
            }
        }
    }

    /// <summary>
    /// Rewrites a method declaration so that its return type is
    /// <c>System.Threading.Tasks.Task</c>.
    /// Trivia from the original return type is preserved.
    /// </summary>
    private static async Task<Document> ChangeMethodReturnTypeToTaskAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken ct)
    {
        var newReturnType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
            .WithTriviaFrom(method.ReturnType);

        var newMethod = method.WithReturnType(newReturnType);

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Rewrites a local function so that its return type is
    /// <c>System.Threading.Tasks.Task</c>.
    /// Trivia from the original return type is preserved.
    /// </summary>
    private static async Task<Document> ChangeLocalFunctionReturnTypeToTaskAsync(
        Document document,
        LocalFunctionStatementSyntax localFunc,
        CancellationToken ct)
    {
        var newReturnType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task")
            .WithTriviaFrom(localFunc.ReturnType);

        var newLocal = localFunc.WithReturnType(newReturnType);

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var newRoot = root.ReplaceNode(localFunc, newLocal);
        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// Converts an explicit delegate type in a variable declaration from a void returning
    /// zero parameter delegate (for example <c>Action</c>) to <c>System.Func&lt;System.Threading.Tasks.Task&gt;</c>.
    /// Performs a semantic check to confirm the current type fits this shape.
    /// </summary>
    private static async Task<Document> ChangeDelegateTypeToFuncTaskAsync(
        Document document,
        VariableDeclarationSyntax variableDecl,
        CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        var typeInfo = semanticModel.GetTypeInfo(variableDecl.Type, ct);
        var typeSymbol = typeInfo.Type as INamedTypeSymbol;
        if (typeSymbol is null)
            return document;

        var invoke = typeSymbol.DelegateInvokeMethod;
        if (invoke is null || !invoke.ReturnsVoid || invoke.Parameters.Length != 0)
            return document;

        var funcTaskType = SyntaxFactory.ParseTypeName("System.Func<System.Threading.Tasks.Task>")
            .WithTriviaFrom(variableDecl.Type);

        var newDecl = variableDecl.WithType(funcTaskType);

        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var newRoot = root.ReplaceNode(variableDecl, newDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}
