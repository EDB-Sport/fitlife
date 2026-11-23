using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// Runs one analyzer over a C# snippet and checks its diagnostics against the snippet's markup.
/// <c>{|TEAM004:text|}</c> marks text where TEAM004 must be reported. Any diagnostic that isn't marked fails the test.
/// </summary>
internal static class AnalyzerVerifier
{
    /// <summary>Compiles <paramref name="source"/> against .NET 10 and fails if the diagnostics differ from the markup.</summary>
    /// <param name="source">The snippet, with markup for every expected diagnostic.</param>
    /// <param name="analyzerConfigFile">Optional .editorconfig or .globalconfig file (path and content) next to the snippet.</param>
    /// <param name="behaviors">Checks of the test framework to skip, for a rule that needs it.</param>
    /// <param name="expected">Diagnostics expected on top of the markup, for checks of the message or severity.</param>
    public static Task VerifyAsync<TAnalyzer>(
        string source,
        (string Path, string Content)? analyzerConfigFile = null,
        TestBehaviors behaviors = TestBehaviors.None,
        params DiagnosticResult[] expected)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // The services target net10.0, so the snippets compile against the same reference assemblies
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
            TestBehaviors = behaviors,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        if (analyzerConfigFile is { } file)
        {
            test.TestState.AnalyzerConfigFiles.Add((file.Path, file.Content));
        }

        return test.RunAsync();
    }
}
