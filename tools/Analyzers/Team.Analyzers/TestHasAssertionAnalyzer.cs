using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Team.Analyzers;

/// <summary>
/// TEAM006: every NUnit test (<c>[Test]</c>, <c>[TestCase]</c>, <c>[TestCaseSource]</c>) contains at least one assertion
/// or a Moq <c>Verify(...)</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestHasAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM006";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Test without an assertion",
        messageFormat: "Found test '{0}' without an assertion. Do add an Assert.That(...) (or a Moq Verify) that fails when the behaviour is wrong, in {1}.",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A test with no assertion always passes (F.I.R.S.T.: Self-validating). Agents write those when they're asked to add tests.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/CSHARP_SETUP_TODO.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    private static readonly ImmutableHashSet<string> TestAttributes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "NUnit.Framework.TestAttribute",
        "NUnit.Framework.TestCaseAttribute",
        "NUnit.Framework.TestCaseSourceAttribute");

    private static readonly ImmutableHashSet<string> AssertNamespaces = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "NUnit.Framework",
        "NUnit.Framework.Legacy");

    // Assert methods that can't fail the test
    private static readonly ImmutableHashSet<string> NonFailingAsserts = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Pass",
        "Ignore",
        "Inconclusive",
        "Warn");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeMethodBody);
    }

    private static void AnalyzeMethodBody(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol method || !method.GetAttributes().Any(IsTestAttribute))
        {
            return;
        }

        bool asserts = context.OperationBlocks
            .SelectMany(block => block.DescendantsAndSelf())
            .OfType<IInvocationOperation>()
            .Any(invocation => IsAssertion(invocation.TargetMethod));
        if (!asserts)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations[0], method.Name, method.ContainingType.Name));
        }
    }

    private static bool IsTestAttribute(AttributeData attribute) =>
        TestAttributes.Contains(attribute.AttributeClass?.ToDisplayString() ?? string.Empty);

    // A call on an NUnit assert class (Assert, ClassicAssert, StringAssert, ...) or a Moq Verify*
    private static bool IsAssertion(IMethodSymbol method)
    {
        INamedTypeSymbol type = method.ContainingType;
        string ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (AssertNamespaces.Contains(ns) && type.Name.EndsWith("Assert", StringComparison.Ordinal))
        {
            return !NonFailingAsserts.Contains(method.Name);
        }

        return string.Equals(ns, "Moq", StringComparison.Ordinal)
            && method.Name.StartsWith("Verify", StringComparison.Ordinal);
    }
}
