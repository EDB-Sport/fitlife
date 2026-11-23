using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM009: a controller action has an explicit HTTP-verb attribute and a typed result
/// (<c>ActionResult&lt;T&gt;</c> or a typed result from <c>Microsoft.AspNetCore.Http.HttpResults</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerActionContractAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Controller action without a verb or a typed result",
        messageFormat: "Found action '{0}' {1}. Do {2}, in {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated OpenAPI contract stays accurate only when every action names its HTTP verb and its result type, so the breaking-change check means something.",
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
