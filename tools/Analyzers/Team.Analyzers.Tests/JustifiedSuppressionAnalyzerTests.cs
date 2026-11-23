namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM004: <c>#pragma warning disable</c> and <c>#nullable disable</c> need a trailing <c>// #&lt;issue&gt;: &lt;reason&gt;</c>,
/// and <c>[SuppressMessage]</c> needs <c>Justification = "#&lt;issue&gt;: &lt;reason&gt;"</c>.
/// </summary>
[TestFixture]
public sealed class JustifiedSuppressionAnalyzerTests
{
    // --- #pragma warning disable ---

    [TestCase("")]
    [TestCase(" // needed for now")]
    [TestCase(" // #12")]
    [TestCase(" // #12:")]
    [TestCase(" // 12: no hash")]
    [TestCase(" // #abc: not a number")]
    public Task PragmaWarningDisable_NoIssueReference_ReportsTeam004(string comment)
    {
        var source = $$"""
            {|TEAM004:#pragma warning disable CS0168|}{{comment}}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task PragmaWarningDisable_AllWarningsWithoutComment_ReportsTeam004()
    {
        const string source = """
            {|TEAM004:#pragma warning disable|}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task PragmaWarningDisable_IssueReferenceOnLineAbove_ReportsTeam004()
    {
        const string source = """
            // #12: the reference must be on the same line as the directive
            {|TEAM004:#pragma warning disable CS0168, CS0219|}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task PragmaWarningDisable_InsideMethod_ReportsTeam004()
    {
        const string source = """
            class C
            {
                void M()
                {
            {|TEAM004:#pragma warning disable CS0168|}
                    int unused;
            #pragma warning restore CS0168
                }
            }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [TestCase(" // #12: Razor needs the generated field")]
    [TestCase(" //#7:no spaces")]
    public Task PragmaWarningDisable_IssueReference_NoDiagnostic(string comment)
    {
        var source = $$"""
            #pragma warning disable CS0168{{comment}}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task PragmaWarningRestore_WithoutComment_NoDiagnostic()
    {
        const string source = """
            #pragma warning restore CS0168
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    // --- #nullable disable ---

    [TestCase("#nullable disable", "")]
    [TestCase("#nullable disable warnings", "")]
    [TestCase("#nullable disable annotations", "")]
    [TestCase("#nullable disable", " // legacy code")]
    public Task NullableDisable_NoIssueReference_ReportsTeam004(string directive, string comment)
    {
        var source = $$"""
            {|TEAM004:{{directive}}|}{{comment}}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task NullableDisable_IssueReference_NoDiagnostic()
    {
        const string source = """
            #nullable disable // #12: generated client, nullable annotations come in #13
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [TestCase("#nullable enable")]
    [TestCase("#nullable restore")]
    [TestCase("#nullable enable warnings")]
    public Task NullableEnableOrRestore_WithoutComment_NoDiagnostic(string directive)
    {
        var source = $$"""
            {{directive}}
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    // --- [SuppressMessage] ---

    [TestCase("SuppressMessage(\"Style\", \"IDE0060\")")]
    [TestCase("SuppressMessageAttribute(\"Style\", \"IDE0060\")")]
    [TestCase("UnconditionalSuppressMessage(\"Trimming\", \"IL2026\")")]
    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = \"\")")]
    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = \"not needed\")")]
    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = \"#12\")")]
    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = \"#12: \")")]
    [TestCase("System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"IDE0060\")")]
    public Task SuppressMessage_NoIssueReference_ReportsTeam004(string attribute)
    {
        var source = $$"""
            using System.Diagnostics.CodeAnalysis;

            [{|TEAM004:{{attribute}}|}]
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task SuppressMessage_AssemblyLevelWithoutJustification_ReportsTeam004()
    {
        const string source = """
            using System.Diagnostics.CodeAnalysis;

            [assembly: {|TEAM004:SuppressMessage("Style", "IDE0060", Scope = "member", Target = "~M:C.M")|}]

            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = \"#12: the parameter is part of the public contract\")")]
    [TestCase("UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"#7: no trimming in containers\")")]
    [TestCase("SuppressMessage(\"Style\", \"IDE0060\", Justification = Reasons.Contract)")]
    public Task SuppressMessage_IssueReference_NoDiagnostic(string attribute)
    {
        var source = $$"""
            using System.Diagnostics.CodeAnalysis;

            [{{attribute}}]
            class C { }

            static class Reasons
            {
                public const string Contract = "#12: the parameter is part of the public contract";
            }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    [Test]
    public Task SuppressMessage_OwnAttributeWithTheSameName_NoDiagnostic()
    {
        const string source = """
            namespace MyCode
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                sealed class SuppressMessageAttribute : System.Attribute
                {
                    public SuppressMessageAttribute(string category, string checkId) { }
                }

                [SuppressMessage("Style", "IDE0060")]
                class C { }
            }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }

    // --- Generated code ---

    [Test]
    public Task GeneratedCode_SuppressionsWithoutIssueReference_NoDiagnostic()
    {
        const string source = """
            // <auto-generated/>
            #pragma warning disable CS1591
            #nullable disable
            using System.Diagnostics.CodeAnalysis;

            [SuppressMessage("Style", "IDE0060")]
            class C { }
            """;
        return AnalyzerVerifier.VerifyAsync<JustifiedSuppressionAnalyzer>(source);
    }
}
