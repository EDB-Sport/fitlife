using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM003: a log call whose arguments or template placeholders are named <c>password</c>, <c>token</c>, <c>secret</c>,
/// <c>connectionString</c>, <c>jwt</c> or <c>cpr</c> (any case, also as part of a longer name) is an error.
/// </summary>
[TestFixture]
public sealed class NoSecretsInLogsAnalyzerTests
{
    // A stand-in for Microsoft.Extensions.Logging.Abstractions, so the snippets need no package reference.
    // It is a project of its own, like the real package.
    private const string LoggingStub = """
        using System;
        namespace Microsoft.Extensions.Logging
        {
            public enum LogLevel { Information, Error }
            public interface ILogger { }
            public interface ILogger<out T> : ILogger { }
            public static class LoggerExtensions
            {
                public static void LogInformation(this ILogger logger, string? message, params object?[] args) { }
                public static void LogError(this ILogger logger, Exception? exception, string? message, params object?[] args) { }
                public static void Log(this ILogger logger, LogLevel logLevel, string? message, params object?[] args) { }
                public static IDisposable? BeginScope(this ILogger logger, string messageFormat, params object?[] args) => null;
            }
        }
        """;

    private static Task VerifyAsync(string source)
    {
        var test = new CSharpAnalyzerTest<NoSecretsInLogsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalProjects["Logging"].Sources.Add(("LoggingStub.cs", LoggingStub));
        test.TestState.AdditionalProjectReferences.Add("Logging");
        return test.RunAsync();
    }

    private static Task VerifyAsync(string source, DiagnosticResult expected)
    {
        var test = new CSharpAnalyzerTest<NoSecretsInLogsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalProjects["Logging"].Sources.Add(("LoggingStub.cs", LoggingStub));
        test.TestState.AdditionalProjectReferences.Add("Logging");
        test.ExpectedDiagnostics.Add(expected);
        return test.RunAsync();
    }

    // --- raises TEAM003: template placeholders ---

    [TestCase("Password")]
    [TestCase("password")]
    [TestCase("Token")]
    [TestCase("Secret")]
    [TestCase("ConnectionString")]
    [TestCase("Jwt")]
    [TestCase("CPR")]
    [TestCase("AccessToken")]
    [TestCase("@Password")]
    public Task Placeholder_WithSecretName_ReportsTeam003(string placeholder)
    {
        string source = $$"""
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger<LoginService> logger, string value)
                {
                    logger.LogInformation({|TEAM003:"Signed in with {{{placeholder}}}"|}, value);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PlaceholderInLogError_ReportsTeam003()
    {
        const string source = """
            using System;
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, Exception exception, string value)
                {
                    logger.LogError(exception, {|TEAM003:"Sign-in failed for {Jwt}"|}, value);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PlaceholderInLogWithLevel_ReportsTeam003()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, string value)
                {
                    logger.Log(LogLevel.Information, {|TEAM003:"Token {Token}"|}, value);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PlaceholderInBeginScope_ReportsTeam003()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, string value)
                {
                    using var scope = logger.BeginScope({|TEAM003:"Session {SessionToken}"|}, value);
                }
            }
            """;
        return VerifyAsync(source);
    }

    // --- raises TEAM003: arguments ---

    [Test]
    public Task Argument_LocalNamedPassword_ReportsTeam003()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, string password)
                {
                    logger.LogInformation("Signed in with {Value}", {|TEAM003:password|});
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task Argument_PropertyNamedConnectionString_ReportsTeam003()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class Options { public string ConnectionString { get; set; } = ""; }

            class Startup
            {
                void M(ILogger logger, Options options)
                {
                    logger.LogInformation("Connecting to {Database}", {|TEAM003:options.ConnectionString|});
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task Argument_MessageSaysWhatToDo()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, string cpr)
                {
                    logger.LogInformation("Member {MemberId}", cpr);
                }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(NoSecretsInLogsAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(7, 52, 7, 55)
            .WithMessage("Found 'cpr' in a log call. Do log an id or a masked value instead of the secret or personal data, in LoginService.M.");
        return VerifyAsync(source, expected);
    }

    // --- reports nothing ---

    [Test]
    public Task OrdinaryPlaceholdersAndArguments_ReportNothing()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class ReservationService
            {
                void M(ILogger<ReservationService> logger, string memberId, int seats)
                {
                    logger.LogInformation("Member {MemberId} reserved {Seats} seats", memberId, seats);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task SecretWordOutsideAPlaceholder_ReportsNothing()
    {
        const string source = """
            using Microsoft.Extensions.Logging;

            class LoginService
            {
                void M(ILogger logger, string memberId)
                {
                    logger.LogInformation("Password reset requested by {MemberId}", memberId);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NotALogCall_ReportsNothing()
    {
        const string source = """
            class Writer
            {
                void LogInformation(string message, params object[] args) { }

                void M(string password)
                {
                    LogInformation("Value {Password}", password);
                }
            }
            """;
        return VerifyAsync(source);
    }
}
