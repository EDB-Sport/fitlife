using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM002: the NLog API (<c>LogManager</c>, <c>NLog.Logger</c>, <c>NLog.ILogger</c>) is used only in <c>Program.cs</c>.
/// Everything else injects <c>ILogger&lt;T&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NlogOnlyInProgramAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM002";

    private const string ProgramFile = "Program.cs";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "NLog API outside Program.cs",
        messageFormat: "Found NLog '{0}' outside Program.cs. Do inject ILogger<T> and log through it, and keep the NLog setup in Program.cs.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "NLog is set up in Program.cs. Other code logs through ILogger<T>, which tests can fake and CA2254 checks.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/TECHNICAL_REFERENCE.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    private static readonly ImmutableHashSet<string> NlogTypes = ImmutableHashSet.Create(StringComparer.Ordinal, "LogManager", "Logger", "ILogger");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context)
    {
        var name = (IdentifierNameSyntax)context.Node;
        if (!NlogTypes.Contains(name.Identifier.ValueText)
            || string.Equals(Path.GetFileName(name.SyntaxTree.FilePath), ProgramFile, StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is INamedTypeSymbol type
            && !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly)
            && type.ContainingNamespace is { Name: "NLog", ContainingNamespace.IsGlobalNamespace: true })
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), type.Name));
        }
    }
}
