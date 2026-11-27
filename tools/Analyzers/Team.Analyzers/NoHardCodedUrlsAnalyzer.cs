using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM010: no string literal containing <c>http://</c>, <c>https://</c>, <c>mongodb://</c> or <c>amqp://</c> in <c>src/</c>.
/// The repo root comes from <c>build_property.RepoRoot</c>, which <c>Directory.Build.props</c> passes in (#533).
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
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/TECHNICAL_REFERENCE.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    private static readonly string[] Schemes = ["http://", "https://", "mongodb://", "amqp://"];

    // The MSBuild property RepoRoot, made visible to analyzers by <CompilerVisibleProperty Include="RepoRoot" /> (#533)
    private const string RepoRootOption = "build_property.RepoRoot";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            start.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(RepoRootOption, out string? repoRoot);
            string? srcPrefix = SrcPrefix(repoRoot);
            start.RegisterSyntaxNodeAction(
                node => AnalyzeString(node, srcPrefix), SyntaxKind.StringLiteralExpression, SyntaxKind.InterpolatedStringText);
        });
    }

    private static void AnalyzeString(SyntaxNodeAnalysisContext context, string? srcPrefix)
    {
        if (!IsUnderSrc(context.Node.SyntaxTree.FilePath, srcPrefix))
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

    // Without RepoRoot (a project outside this repo's Directory.Build.props): the last of the repo's top-level folders in the
    // path decides. That guess is wrong for a file in libs/ or at the root of a clone under a folder named src (#533), so the
    // build always passes RepoRoot in.
    private static readonly string[] TopLevelFolders = ["src", "tests", "tools"];

    // "<repo root>/src/" with forward slashes, or null without a repo root
    private static string? SrcPrefix(string? repoRoot)
    {
        if (repoRoot is null || repoRoot.Trim().Length == 0)
        {
            return null;
        }

        return Normalize(repoRoot).TrimEnd('/') + "/src/";
    }

    // A file in the repo's src/ folder, on any OS: /repo/src/... or C:\repo\src\... With a repo root, the folder right after
    // it decides, so ~/src/fitlife/libs/... is not under src/. Paths compare without case, as on Windows.
    private static bool IsUnderSrc(string path, string? srcPrefix)
    {
        if (srcPrefix is not null)
        {
            return Normalize(path).StartsWith(srcPrefix, StringComparison.OrdinalIgnoreCase);
        }

        string? last = path.Split('/', '\\').LastOrDefault(segment => TopLevelFolders.Contains(segment, StringComparer.Ordinal));
        return string.Equals(last, "src", StringComparison.Ordinal);
    }

    private static string Normalize(string path)
    {
        return path.Trim().Replace('\\', '/');
    }
}
