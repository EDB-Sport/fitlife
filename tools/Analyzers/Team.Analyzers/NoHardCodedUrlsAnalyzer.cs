using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM010: no string literal containing <c>http://</c>, <c>https://</c>, <c>mongodb://</c> or <c>amqp://</c> in <c>src/</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoHardCodedUrlsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM010";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Hard-coded address in src/",
        messageFormat: "Found the hard-coded address \"{0}\". Do read it from configuration (appsettings, environment or Vault) instead, in {1}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Course rule #13: no hard-coded URLs, so components can be moved. It also catches connection strings that belong in Vault (rule #9).",
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
