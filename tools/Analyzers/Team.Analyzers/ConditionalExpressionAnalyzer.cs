using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM012: a conditional expression (<c>?:</c>) is not used as a return value, an assigned value or a variable initializer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalExpressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM012";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Conditional expression in place of if/return",
        messageFormat: "Found a conditional expression (?:) as {0}. Do write an if statement with {1} in each branch in {2} instead.",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "One readable style that agents copy: an if statement with a return or an assignment in each branch, not a conditional expression.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/issues/60");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConditional, SyntaxKind.ConditionalExpression);
    }

    private static void AnalyzeConditional(SyntaxNodeAnalysisContext context)
    {
        SyntaxNode node = context.Node;
        SyntaxNode? parent = node.Parent;
        while (parent is ParenthesizedExpressionSyntax)
        {
            node = parent;
            parent = parent.Parent;
        }

        // Only the outermost conditional is reported: an inner one goes away with the outer one's if statement
        string[]? use = parent switch
        {
            ReturnStatementSyntax => ["a return value", "a return", "the method"],
            AssignmentExpressionSyntax assignment when assignment.Right == node => ["an assigned value", "an assignment", "the method"],
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax } }
                => ["a field initializer", "an assignment", "the constructor"],
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax } => ["a variable initializer", "an assignment", "the method"],
            _ => null,
        };
        if (use is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), use[0], use[1], use[2]));
        }
    }
}
