using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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

    private const string LoggerExtensions = "Microsoft.Extensions.Logging.LoggerExtensions";

    // The template parameter is "message" on Log* and "messageFormat" on BeginScope
    private static readonly ImmutableHashSet<string> TemplateParameters = ImmutableHashSet.Create(StringComparer.Ordinal, "message", "messageFormat");

    private static readonly string[] SecretWords = ["password", "token", "secret", "connectionstring", "jwt", "cpr"];

    // "{Name}", "{@Name}", "{Name,10}" and "{Name:format}": the name is what is between the brace (and @ or $) and , or :
    private static readonly Regex Placeholder = new(
        "\\{[@$]?(?<name>[^{},:]+)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        if (!string.Equals(invocation.TargetMethod.ContainingType?.ToDisplayString(), LoggerExtensions, StringComparison.Ordinal))
        {
            return;
        }

        string where = $"{context.ContainingSymbol.ContainingType?.Name}.{context.ContainingSymbol.Name}";
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (TemplateParameters.Contains(argument.Parameter?.Name ?? string.Empty))
            {
                CheckTemplate(context, argument.Value, where);
            }

            if (argument.ArgumentKind == ArgumentKind.ParamArray && argument.Value is IArrayCreationOperation { Initializer: { } values })
            {
                foreach (IOperation value in values.ElementValues)
                {
                    CheckArgument(context, value, where);
                }
            }
        }
    }

    private static void CheckTemplate(OperationAnalysisContext context, IOperation template, string where)
    {
        if (template.ConstantValue is not { HasValue: true, Value: string text })
        {
            return;
        }

        foreach (Match match in Placeholder.Matches(text))
        {
            string name = match.Groups["name"].Value.Trim();
            if (IsSecret(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, template.Syntax.GetLocation(), name, where));
                return;
            }
        }
    }

    private static void CheckArgument(OperationAnalysisContext context, IOperation value, string where)
    {
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        string? name = value switch
        {
            ILocalReferenceOperation local => local.Local.Name,
            IParameterReferenceOperation parameter => parameter.Parameter.Name,
            IFieldReferenceOperation field => field.Field.Name,
            IPropertyReferenceOperation property => property.Property.Name,
            _ => null,
        };
        if (name is not null && IsSecret(name))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, value.Syntax.GetLocation(), name, where));
        }
    }

    // Any case, also as part of a longer name: AccessToken, userPassword, DbConnectionString
    private static bool IsSecret(string name) =>
        SecretWords.Any(word => name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
}
