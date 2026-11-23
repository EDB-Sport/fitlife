using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM008: every public type in a <c>*.Models</c> namespace is in the "In code" column of <c>GLOSSARY.md</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModelTypesInGlossaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM008";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Model type not in the glossary",
        messageFormat: "Found model type '{0}', {1}",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Entity names come from schema.org, in English, and the code follows the ubiquitous language in GLOSSARY.md (course rules #5-6).",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/GLOSSARY.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
