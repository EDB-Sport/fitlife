using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM007: no <c>Thread.Sleep</c> and no <c>Task.Delay</c> in test projects (<c>*.Tests</c>, <c>*.IntegrationTests</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoWaitInTestsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Real waiting in a test",
        messageFormat: "Found {0} in test project {1}. Do advance a FakeTimeProvider (Microsoft.Extensions.Time.Testing) instead of waiting, in the test and in the code under test.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "F.I.R.S.T.: a test that sleeps is slow, and one that waits on the real clock is not repeatable.",
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
