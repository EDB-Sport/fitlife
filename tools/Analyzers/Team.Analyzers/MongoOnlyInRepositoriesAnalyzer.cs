using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM001: <c>MongoDB.Driver</c> types are used only in a <c>*.Repositories</c> namespace.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MongoOnlyInRepositoriesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "MongoDB.Driver outside a repository",
        messageFormat: "Found MongoDB.Driver type '{0}' in {1}. Do the database work in a repository class in a *.Repositories namespace, and use its interface here.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Course MUST: MongoDB only through repository interfaces, never from controllers, so controllers can be tested with a stub repository.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/CSHARP_SETUP_TODO.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
    }

    private static void AnalyzeName(SyntaxNodeAnalysisContext context)
    {
        var name = (SimpleNameSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is not INamedTypeSymbol type
            || !IsDriverNamespace(type.ContainingNamespace))
        {
            return;
        }

        INamespaceSymbol? where = context.ContainingSymbol as INamespaceSymbol ?? context.ContainingSymbol?.ContainingNamespace;
        // The repositories may use the driver, and so may the driver's own code
        if (where is null || IsRepositoriesNamespace(where) || IsDriverNamespace(where))
        {
            return;
        }

        string whereText = where.ToDisplayString();
        if (where.IsGlobalNamespace)
        {
            whereText = "the global namespace";
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation(), type.Name, whereText));
    }

    // MongoDB.Driver and its sub-namespaces (MongoDB.Driver.Linq, ...), not MongoDB.Bson
    private static bool IsDriverNamespace(INamespaceSymbol? ns)
    {
        string text = ns?.ToDisplayString() ?? string.Empty;
        return string.Equals(text, "MongoDB.Driver", StringComparison.Ordinal)
            || text.StartsWith("MongoDB.Driver.", StringComparison.Ordinal);
    }

    // BookingService.Repositories: the last segment of the namespace
    private static bool IsRepositoriesNamespace(INamespaceSymbol ns)
    {
        return string.Equals(ns.Name, "Repositories", StringComparison.Ordinal);
    }
}
