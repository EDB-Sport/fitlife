using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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
    }
}
