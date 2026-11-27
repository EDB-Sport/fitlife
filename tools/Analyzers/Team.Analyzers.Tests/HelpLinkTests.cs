using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers.Tests;

/// <summary>
/// The help link of the TEAM rules described in section 4 of <c>docs/TECHNICAL_REFERENCE.md</c> opens that section (#516),
/// not the moved setup list.
/// </summary>
[TestFixture]
public sealed class HelpLinkTests
{
    private const string SectionFour = "https://github.com/EDB-Sport/fitlife/blob/develop/docs/TECHNICAL_REFERENCE.md#4-project-specific-roslyn-analyzers-toolsanalyzers";

    private static readonly DiagnosticAnalyzer[] SectionFourAnalyzers =
    [
        new MongoOnlyInRepositoriesAnalyzer(),
        new NlogOnlyInProgramAnalyzer(),
        new NoSecretsInLogsAnalyzer(),
        new JustifiedSuppressionAnalyzer(),
        new ReadabilityLimitsAnalyzer(),
        new TestHasAssertionAnalyzer(),
        new NoWaitInTestsAnalyzer(),
        new ControllerActionContractAnalyzer(),
        new NoHardCodedUrlsAnalyzer(),
    ];

    // Their links open their own issue or the glossary
    private static readonly DiagnosticAnalyzer[] OtherAnalyzers =
    [
        new ConditionalExpressionAnalyzer(),
        new ModelTypesInGlossaryAnalyzer(),
    ];

    [TestCaseSource(nameof(SectionFourAnalyzers))]
    public void HelpLinkUri_OfARuleInSectionFour_OpensTechnicalReferenceSectionFour(DiagnosticAnalyzer analyzer)
    {
        // Act
        string?[] links = [.. analyzer.SupportedDiagnostics.Select(rule => rule.HelpLinkUri)];

        // Assert
        Assert.That(links, Is.All.EqualTo(SectionFour));
    }

    [TestCaseSource(nameof(OtherAnalyzers))]
    [TestCaseSource(nameof(SectionFourAnalyzers))]
    public void HelpLinkUri_OfEveryRule_NeverOpensTheMovedSetupList(DiagnosticAnalyzer analyzer)
    {
        // Act
        string[] links = [.. analyzer.SupportedDiagnostics.Select(rule => rule.HelpLinkUri ?? string.Empty)];

        // Assert
        Assert.That(links, Has.None.Contains("SETUP_TODO"));
    }
}
