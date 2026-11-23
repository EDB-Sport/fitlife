using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM005: code every member can explain line by line. No <c>dynamic</c>, no <c>goto</c>, no LINQ query syntax,
/// no nested conditional expressions, and lambdas nested at most one level deep.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadabilityLimitsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Hard-to-read construct",
        messageFormat: "Found {0}. Do {1} instead, in {2}.",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Code every member can explain line by line at the exam: no dynamic, no goto, no LINQ query syntax, no nested conditional expressions, and lambdas nested at most one level deep.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/CSHARP_SETUP_TODO.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDynamic, SyntaxKind.IdentifierName);
        context.RegisterSyntaxNodeAction(
            nodeContext => Report(nodeContext, "'goto'", "use a loop, an if statement or an early return"),
            SyntaxKind.GotoStatement,
            SyntaxKind.GotoCaseStatement,
            SyntaxKind.GotoDefaultStatement);
        context.RegisterSyntaxNodeAction(
            nodeContext => Report(nodeContext, "LINQ query syntax", "use LINQ method syntax (Where, Select, ...)"),
            SyntaxKind.QueryExpression);
        context.RegisterSyntaxNodeAction(AnalyzeConditional, SyntaxKind.ConditionalExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeAnonymousFunction,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.AnonymousMethodExpression);
    }

    private static void AnalyzeDynamic(SyntaxNodeAnalysisContext context)
    {
        IdentifierNameSyntax name = (IdentifierNameSyntax)context.Node;
        if (string.Equals(name.Identifier.ValueText, "dynamic", StringComparison.Ordinal)
            && context.SemanticModel.GetTypeInfo(name, context.CancellationToken).Type?.TypeKind == TypeKind.Dynamic)
        {
            Report(context, "'dynamic'", "use a static type, or a class for the data's shape");
        }
    }

    // The inner conditional is reported: the outer one alone is allowed
    private static void AnalyzeConditional(SyntaxNodeAnalysisContext context)
    {
        SyntaxNode? parent = context.Node.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            parent = parent.Parent;
        }

        if (parent is ConditionalExpressionSyntax)
        {
            Report(context, "a nested conditional expression (?:)", "use an if statement, or a method with a name for the inner choice");
        }
    }

    // A lambda inside a lambda is fine. A third level is reported.
    private static void AnalyzeAnonymousFunction(SyntaxNodeAnalysisContext context)
    {
        int enclosing = context.Node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Count();
        if (enclosing >= 2)
        {
            Report(context, "a lambda nested more than one level deep", "move the inner lambda into a method with a name");
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, string found, string fix)
    {
        ISymbol? member = context.ContainingSymbol;
        while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            member = member.ContainingSymbol;
        }

        string where = $"{member?.ContainingType?.Name}.{member?.Name}";
        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), found, fix, where));
    }
}
