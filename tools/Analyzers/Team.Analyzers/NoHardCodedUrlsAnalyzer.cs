using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    private static readonly string[] Schemes = ["http://", "https://", "mongodb://", "amqp://"];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeString, SyntaxKind.StringLiteralExpression, SyntaxKind.InterpolatedStringText);
    }

    private static void AnalyzeString(SyntaxNodeAnalysisContext context)
    {
        if (!IsUnderSrc(context.Node.SyntaxTree.FilePath))
        {
            return;
        }

        // Plain, verbatim and raw literals, and the text parts of an interpolated string
        string text = context.Node switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            InterpolatedStringTextSyntax part => part.TextToken.ValueText,
            _ => string.Empty,
        };
        if (!Schemes.Any(scheme => text.IndexOf(scheme, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return;
        }

        ISymbol? member = context.ContainingSymbol;
        string where = (member as INamedTypeSymbol ?? member?.ContainingType)?.Name ?? string.Empty;
        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), text, where));
    }

    // A file in a "src" folder of the repo, on any OS: /repo/src/... or C:\repo\src\...
    private static bool IsUnderSrc(string path) =>
        path.Split('/', '\\').Any(segment => string.Equals(segment, "src", StringComparison.Ordinal));
}
