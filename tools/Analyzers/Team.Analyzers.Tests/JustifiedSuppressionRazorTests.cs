using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM004 on Razor's C# output. <c>.editorconfig</c> marks <c>*_razor.g.cs</c> as hand-written, so the style rules check
/// <c>.razor</c> <c>@code</c> blocks (#397). The <c>#pragma</c> and <c>#nullable</c> lines in that output come from the Razor
/// generator, not from the team, so TEAM004 skips them.
/// </summary>
[TestFixture]
public sealed class JustifiedSuppressionRazorTests
{
    private const string NotGenerated = "root = true\n\n[*_razor.g.cs]\ngenerated_code = false\n\n[*.cs]\ngenerated_code = false\n";

    [Test]
    public Task RazorOutput_MarkedNotGenerated_NoDiagnosticForTheGeneratorsDirectives()
    {
        const string source = """
            #pragma warning disable 1591
            #nullable disable
            namespace Portal.Components.Pages
            {
                public partial class Home { }
            }
            """;
        return VerifyAtPathAsync("/0/Components/Pages/Home_razor.g.cs", source);
    }

    [Test]
    public Task HandWrittenFile_MarkedNotGenerated_StillReportsTeam004()
    {
        const string source = """
            {|TEAM004:#nullable disable|}
            class C { }
            """;
        return VerifyAtPathAsync("/0/Components/Pages/Home.cs", source);
    }

    // A snippet at its own path, because the rule depends on the file name
    private static Task VerifyAtPathAsync(string path, string source)
    {
        var test = new CSharpAnalyzerTest<JustifiedSuppressionAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            // TEAM004 reports the bare pragma the framework's suppression check adds (see JustifiedSuppressionAnalyzerTests)
            TestBehaviors = TestBehaviors.SkipSuppressionCheck,
        };
        test.TestState.Sources.Add((path, source));
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", NotGenerated));
        return test.RunAsync();
    }
}
