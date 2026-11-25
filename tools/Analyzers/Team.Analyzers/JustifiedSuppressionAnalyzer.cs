using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Team.Analyzers;

/// <summary>
/// TEAM004: every switched-off check names the GitHub issue that allows it.
/// <c>#pragma warning disable</c> and <c>#nullable disable</c> need a trailing <c>// #&lt;issue&gt;: &lt;reason&gt;</c>
/// on the same line, and <c>[SuppressMessage]</c> needs <c>Justification = "#&lt;issue&gt;: &lt;reason&gt;"</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JustifiedSuppressionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM004";

    // TEAM004 can't be switched off the way it forbids:
    // NotConfigurable ignores '#pragma warning disable TEAM004' and severity lines in .editorconfig/.globalconfig,
    // Compiler makes Roslyn ignore [SuppressMessage(..., "TEAM004")], as it does for compiler warnings.
    private static readonly string[] Unsuppressible = [WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.Compiler];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Suppression without an issue reference",
        messageFormat: "Found {0} without an issue reference. Add {1}, naming the GitHub issue that allows the exception.",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A pragma, a '#nullable disable' or a [SuppressMessage] switches a check off. Each one must name the GitHub issue that allows it, so every exception is traceable and reviewed.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/CSHARP_SETUP_TODO.md#4-project-specific-roslyn-analyzers-toolsanalyzers",
        customTags: Unsuppressible);

    private const string CommentFix = "'// #<issue>: <reason>' at the end of the same line";
    private const string JustificationFix = "Justification = \"#<issue>: <reason>\" to the attribute";

    // "#12: reason": a hash, the issue number, a colon, then the reason
    private static readonly Regex IssueReference = new(
        "^#[0-9]+:\\s*\\S",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly ImmutableHashSet<string> SuppressionAttributes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Generated code (Razor, OpenAPI clients) has its own pragmas, which nobody on the team wrote
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeDirectives);
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    // .editorconfig marks Razor's output as hand-written so the style rules check @code blocks (#397).
    // Its #pragma and #nullable lines are written by the Razor generator, so TEAM004 still skips that file.
    private static bool IsRazorOutput(SyntaxTree tree)
    {
        return tree.FilePath.EndsWith("_razor.g.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static void AnalyzeDirectives(SyntaxTreeAnalysisContext context)
    {
        if (IsRazorOutput(context.Tree))
        {
            return;
        }

        SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);
        for (DirectiveTriviaSyntax? directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            string? found = DisablingDirectiveName(directive);
            if (found is not null && !HasIssueComment(directive))
            {
                var location = Location.Create(context.Tree, SpanWithoutComment(directive));
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, found, CommentFix));
            }
        }
    }

    // The name to show in the message, or null when the directive doesn't switch anything off
    private static string? DisablingDirectiveName(DirectiveTriviaSyntax directive)
    {
        return directive switch
        {
            PragmaWarningDirectiveTriviaSyntax pragma when pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)
                => "'#pragma warning disable'",
            NullableDirectiveTriviaSyntax nullable when nullable.SettingToken.IsKind(SyntaxKind.DisableKeyword)
                => "'#nullable disable'",
            _ => null,
        };
    }

    // A comment inside a directive can only be the one at the end of its line
    private static bool HasIssueComment(DirectiveTriviaSyntax directive)
    {
        return directive.DescendantTrivia().Any(trivia =>
            trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            && IssueReference.IsMatch(trivia.ToString().TrimStart('/').Trim()));
    }

    // From '#' to the last word of the directive, so the error doesn't underline a comment
    private static TextSpan SpanWithoutComment(DirectiveTriviaSyntax directive)
    {
        SyntaxToken lastToken = directive.DescendantTokens().Last(token => !token.IsKind(SyntaxKind.EndOfDirectiveToken));
        return TextSpan.FromBounds(directive.SpanStart, lastToken.Span.End);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        ISymbol? constructor = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol;
        INamedTypeSymbol? attributeType = constructor?.ContainingType;
        if (attributeType is null || !SuppressionAttributes.Contains(attributeType.ToDisplayString()))
        {
            return;
        }

        AttributeArgumentSyntax? justification = attribute.ArgumentList?.Arguments
            .FirstOrDefault(argument => string.Equals(argument.NameEquals?.Name.Identifier.ValueText, "Justification", StringComparison.Ordinal));
        string? text = justification is null
            ? null
            : context.SemanticModel.GetConstantValue(justification.Expression, context.CancellationToken).Value as string;

        if (text is null || !IssueReference.IsMatch(text))
        {
            string found = $"[{attributeType.Name.Replace("Attribute", string.Empty)}]";
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation(), found, JustificationFix));
        }
    }
}
