using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM010: no string literal containing <c>http://</c>, <c>https://</c>, <c>mongodb://</c> or <c>amqp://</c> in <c>src/</c>.
/// Read the address from configuration.
/// </summary>
[TestFixture]
public sealed class NoHardCodedUrlsAnalyzerTests
{
    private const string ServiceFile = "/repo/src/BookingService/Clients/MemberClient.cs";

    private static Task VerifyAsync(string source, string path = ServiceFile, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<NoHardCodedUrlsAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.Sources.Add((path, source));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM010 ---

    [TestCase("http://member-service:8080/members")]
    [TestCase("https://fitlife.example.com")]
    [TestCase("mongodb://mongo:27017")]
    [TestCase("amqp://guest:guest@rabbitmq:5672")]
    [TestCase("HTTPS://FITLIFE.EXAMPLE.COM")]
    [TestCase("see http://member-service for details")]
    public Task UrlInStringLiteral_ReportsTeam010(string address)
    {
        string source = $$"""
            class MemberClient
            {
                private const string Address = {|TEAM010:"{{address}}"|};
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task VerbatimAndRawLiterals_ReportTeam010()
    {
        const string source = """"
            class MemberClient
            {
                private readonly string _first = {|TEAM010:@"http://member-service"|};
                private readonly string _second = {|TEAM010:"""https://fitlife.example.com"""|};
            }
            """";
        return VerifyAsync(source);
    }

    [Test]
    public Task UrlInInterpolatedString_ReportsTeam010()
    {
        const string source = """
            class MemberClient
            {
                string Address(int port) => $"{|TEAM010:http://member-service:|}{port}";
            }
            """;
        return VerifyAsync(source);
    }

    [TestCase("/repo/src/BookingService/Program.cs")]
    [TestCase(@"C:\repo\src\BookingService\Program.cs")]
    [TestCase("/home/dev/src/fitlife/src/BookingService/Program.cs")]
    public Task AnyFileUnderSrc_ReportsTeam010(string path)
    {
        const string source = """
            class Program
            {
                private const string Address = {|TEAM010:"http://member-service"|};
            }
            """;
        return VerifyAsync(source, path);
    }

    [Test]
    public Task UrlInSrc_MessageSaysWhatToDo()
    {
        const string source = """
            class MemberClient
            {
                private const string Address = "http://member-service";
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(NoHardCodedUrlsAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(ServiceFile, 3, 36, 3, 59)
            .WithMessage("Found the hard-coded address \"http://member-service\". Do read it from configuration (appsettings, environment or Vault) instead, in MemberClient.");
        return VerifyAsync(source, ServiceFile, expected);
    }

    // --- reports nothing ---

    [TestCase("/repo/tests/BookingService.Tests/MemberClientTests.cs")]
    [TestCase("/repo/tools/Analyzers/Team.Analyzers/Rule.cs")]
    [TestCase("/repo/sources/BookingService/MemberClient.cs")]
    [TestCase("/home/dev/src/fitlife/tests/BookingService.Tests/MemberClientTests.cs")]
    [TestCase("/home/dev/src/fitlife/tools/Analyzers/Team.Analyzers/Rule.cs")]
    public Task UrlOutsideSrc_ReportsNothing(string path)
    {
        const string source = """
            class MemberClient
            {
                private const string Address = "http://member-service";
            }
            """;
        return VerifyAsync(source, path);
    }

    [TestCase("/members/{id}")]
    [TestCase("ConnectionStrings:Booking")]
    [TestCase("http")]
    [TestCase("ftp://files.example.com")]
    public Task StringWithoutAnAddress_ReportsNothing(string text)
    {
        string source = $$"""
            class MemberClient
            {
                private const string Value = "{{text}}";
            }
            """;
        return VerifyAsync(source);
    }
}
