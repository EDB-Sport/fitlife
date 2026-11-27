using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM010 decides by the folder right after the repo root, which <c>Directory.Build.props</c> passes in as
/// <c>build_property.RepoRoot</c> (#533). A clone in a folder named <c>src</c>, such as <c>~/src/fitlife</c>, no longer
/// makes a file in <c>libs/</c> or at the repo root count as <c>src/</c>.
/// </summary>
[TestFixture]
public sealed class NoHardCodedUrlsRepoRootTests
{
    private const string LinuxRoot = "/home/dev/src/fitlife/";
    private const string WindowsRoot = @"C:\Users\dev\src\fitlife\";

    private const string Reported = """
        class EventSchema
        {
            private const string Draft = {|TEAM010:"https://json-schema.org/draft/2020-12/schema"|};
        }
        """;

    private const string NotReported = """
        class EventSchema
        {
            private const string Draft = "https://json-schema.org/draft/2020-12/schema";
        }
        """;

    [TestCase(LinuxRoot, "/home/dev/src/fitlife/libs/FitLife.Messaging/EventSchema.cs")]
    [TestCase(LinuxRoot, "/home/dev/src/fitlife/EventSchema.cs")]
    [TestCase(LinuxRoot, "/home/dev/src/fitlife/tests/FitLife.Messaging.Tests/EventSchemaTests.cs")]
    [TestCase(LinuxRoot, "/home/dev/src/fitlife/tools/EventSchemaGenerator/Program.cs")]
    [TestCase(WindowsRoot, @"C:\Users\dev\src\fitlife\libs\FitLife.Messaging\EventSchema.cs")]
    [TestCase("/src/", "/src/libs/FitLife.Messaging/EventSchema.cs")]
    public Task UrlOutsideTheRepoSrc_InACloneUnderASrcFolder_ReportsNothing(string repoRoot, string path)
    {
        return VerifyAsync(NotReported, path, repoRoot);
    }

    [TestCase(LinuxRoot, "/home/dev/src/fitlife/src/BookingService/Clients/MemberClient.cs")]
    [TestCase(WindowsRoot, @"C:\Users\dev\src\fitlife\src\BookingService\Clients\MemberClient.cs")]
    [TestCase("/home/dev/src/fitlife", "/home/dev/src/fitlife/src/BookingService/Clients/MemberClient.cs")]
    [TestCase(@"C:\Users\dev\src\fitlife\", @"c:\users\dev\src\fitlife\src\BookingService\MemberClient.cs")]
    [TestCase("/src/", "/src/src/MembershipService/Program.cs")]
    public Task UrlInTheRepoSrc_InACloneUnderASrcFolder_ReportsTeam010(string repoRoot, string path)
    {
        return VerifyAsync(Reported, path, repoRoot);
    }

    private static Task VerifyAsync(string source, string path, string repoRoot)
    {
        var test = new CSharpAnalyzerTest<NoHardCodedUrlsAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.Sources.Add((path, source));
        // What Directory.Build.props passes in through <CompilerVisibleProperty Include="RepoRoot" />
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", $"""
            is_global = true
            build_property.RepoRoot = {repoRoot}
            """));
        return test.RunAsync();
    }
}
