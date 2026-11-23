using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM006: every NUnit test (<c>[Test]</c>, <c>[TestCase]</c>, <c>[TestCaseSource]</c>) contains at least one assertion
/// or a Moq <c>Verify(...)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestHasAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Test without an assertion",
        messageFormat: "Found test '{0}' without an assertion. Do add an Assert.That(...) (or a Moq Verify) that fails when the behaviour is wrong, in {1}.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A test with no assertion always passes (F.I.R.S.T.: Self-validating). Agents write those when they're asked to add tests.",
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
