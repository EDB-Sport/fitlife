using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM003: no secrets or personal data in log calls. A placeholder or argument whose name contains
/// <c>password</c>, <c>token</c>, <c>secret</c>, <c>connectionString</c>, <c>jwt</c> or <c>cpr</c> (any case) is an error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoSecretsInLogsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Secret or personal data in a log call",
        messageFormat: "Found '{0}' in a log call. Do log an id or a masked value instead of the secret or personal data, in {1}.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Logs go to Loki, where everyone with Grafana access can read them. Never log secrets or personal data (course M9).",
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
