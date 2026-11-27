using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Team.Analyzers;

/// <summary>
/// TEAM007: no <c>Thread.Sleep</c> and no <c>Task.Delay</c> in test projects (<c>*.Tests</c>, <c>*.IntegrationTests</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoWaitInTestsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM007";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Real waiting in a test",
        messageFormat: "Found {0} in test project {1}. Do advance a FakeTimeProvider (Microsoft.Extensions.Time.Testing) instead of waiting, in the test and in the code under test.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "F.I.R.S.T.: a test that sleeps is slow, and one that waits on the real clock is not repeatable.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/TECHNICAL_REFERENCE.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            string assemblyName = start.Compilation.AssemblyName ?? string.Empty;
            if (!IsTestProject(assemblyName))
            {
                return;
            }

            INamedTypeSymbol? thread = start.Compilation.GetTypeByMetadataName("System.Threading.Thread");
            INamedTypeSymbol? task = start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            INamedTypeSymbol? timeProvider = start.Compilation.GetTypeByMetadataName("System.TimeProvider");
            start.RegisterOperationAction(
                operation => AnalyzeInvocation(operation, assemblyName, thread, task, timeProvider),
                OperationKind.Invocation);
        });
    }

    // The same convention as tests/Directory.Build.props: tests/<Service>.Tests and tests/<Service>.IntegrationTests
    private static bool IsTestProject(string assemblyName)
    {
        return assemblyName.EndsWith(".Tests", StringComparison.Ordinal)
            || assemblyName.EndsWith(".IntegrationTests", StringComparison.Ordinal);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        string assemblyName,
        INamedTypeSymbol? thread,
        INamedTypeSymbol? task,
        INamedTypeSymbol? timeProvider)
    {
        IMethodSymbol method = ((IInvocationOperation)context.Operation).TargetMethod;
        string? found = null;
        if (string.Equals(method.Name, "Sleep", StringComparison.Ordinal) && SymbolEqualityComparer.Default.Equals(method.ContainingType, thread))
        {
            found = "Thread.Sleep";
        }

        // Task.Delay(delay, timeProvider) is how FakeTimeProvider controls a delay, so it stays allowed
        if (string.Equals(method.Name, "Delay", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, task)
            && !method.Parameters.Any(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, timeProvider)))
        {
            found = "Task.Delay";
        }

        if (found is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), found, assemblyName));
        }
    }
}
