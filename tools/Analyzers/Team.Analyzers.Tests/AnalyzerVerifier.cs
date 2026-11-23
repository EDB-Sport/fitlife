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
    public static Task VerifyAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // The services target net10.0, so the snippets compile against the same reference assemblies
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        return test.RunAsync();
    }
}
