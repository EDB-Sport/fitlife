using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM008: every public type in a <c>*.Models</c> namespace is in the "In code" column of <c>GLOSSARY.md</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModelTypesInGlossaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM008";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Model type not in the glossary",
        messageFormat: "Found model type '{0}', {1}",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Entity names come from schema.org, in English, and the code follows the ubiquitous language in GLOSSARY.md (course rules #5-6).",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/GLOSSARY.md");

    private const string GlossaryFile = "GLOSSARY.md";
    private const string InCodeColumn = "In code";

    private const string NotInGlossary =
        "which is not in the 'In code' column of GLOSSARY.md. Do rename it to the glossary's name for the concept, or add a row for it to GLOSSARY.md, with its schema.org type, in the same PR.";

    private const string NoGlossary =
        "but GLOSSARY.md is not an AdditionalFile of this project. Do add <AdditionalFiles Include=\"GLOSSARY.md\" /> (the repo root's) in the root Directory.Build.props.";

    // `ClassSession`: the names in a cell are in backticks
    private static readonly Regex CodeName = new(
        "`(?<name>[^`]+)`",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            AdditionalText? glossary = start.Options.AdditionalFiles.FirstOrDefault(file =>
                string.Equals(Path.GetFileName(file.Path), GlossaryFile, StringComparison.OrdinalIgnoreCase));
            ImmutableHashSet<string>? names = null;
            if (glossary is not null)
            {
                names = ReadInCodeNames(glossary.GetText(start.CancellationToken)?.ToString() ?? string.Empty);
            }

            start.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, names), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, ImmutableHashSet<string>? names)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (type.DeclaredAccessibility != Accessibility.Public
            || type.ContainingType is not null
            || !string.Equals(type.ContainingNamespace?.Name, "Models", StringComparison.Ordinal))
        {
            return;
        }

        if (names is not null && names.Contains(type.Name))
        {
            return;
        }

        string reason = NotInGlossary;
        if (names is null)
        {
            reason = NoGlossary;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name, reason));
    }

    // Every backticked name in the "In code" column of every table in the file
    private static ImmutableHashSet<string> ReadInCodeNames(string markdown)
    {
        ImmutableHashSet<string>.Builder names = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        int column = -1;
        foreach (string rawLine in markdown.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                column = -1;
                continue;
            }

            string[] cells = line.Trim('|').Split('|');
            int header = Array.FindIndex(cells, cell => string.Equals(cell.Trim(), InCodeColumn, StringComparison.Ordinal));
            if (header >= 0)
            {
                column = header;
                continue;
            }

            if (column >= 0 && column < cells.Length)
            {
                AddCodeNames(cells[column], names);
            }
        }

        return names.ToImmutable();
    }

    private static void AddCodeNames(string cell, ImmutableHashSet<string>.Builder names)
    {
        foreach (Match match in CodeName.Matches(cell))
        {
            names.Add(StripGenericArguments(match.Groups["name"].Value.Trim()));
        }
    }

    // `Reservation<TId>` in the glossary names the type Reservation
    private static string StripGenericArguments(string name)
    {
        int angle = name.IndexOf('<');
        if (angle < 0)
        {
            return name;
        }

        return name.Substring(0, angle);
    }
}
