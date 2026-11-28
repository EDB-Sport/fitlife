using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM011: no public <c>set</c> accessor on a property of a type in a <c>*.Domain</c> namespace (METHODOLOGY 11.2 rule 4).
/// <c>init</c> accessors (positional records, value objects) and private, protected or internal setters are allowed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoPublicSettersInDomainAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM011";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Public setter in the domain model",
        messageFormat: "Found a public setter on '{0}' in {1}. Do change the state through a method on the aggregate root, and make the setter private (or drop it) (METHODOLOGY 11.2 rule 4).",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Aggregates protect their invariants: state changes only through methods on the root, with no public setters in *.Domain (METHODOLOGY 11.2).",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/METHODOLOGY.md#112-rules");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSetter, SyntaxKind.SetAccessorDeclaration);
    }

    private static void AnalyzeSetter(SyntaxNodeAnalysisContext context)
    {
        var accessor = (AccessorDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(accessor, context.CancellationToken) is not IMethodSymbol setter
            || setter.DeclaredAccessibility != Accessibility.Public
            || setter.AssociatedSymbol is not IPropertySymbol property)
        {
            return;
        }

        INamedTypeSymbol type = property.ContainingType;
        INamespaceSymbol? ns = type.ContainingNamespace;
        if (ns is null || !string.Equals(ns.Name, "Domain", StringComparison.Ordinal))
        {
            return;
        }

        string name = $"{type.Name}.{property.Name}";
        context.ReportDiagnostic(Diagnostic.Create(Rule, accessor.GetLocation(), name, ns.ToDisplayString()));
    }
}
