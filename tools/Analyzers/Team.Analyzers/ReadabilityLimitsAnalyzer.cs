using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
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
    }
}
