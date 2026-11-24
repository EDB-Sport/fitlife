using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM002: the NLog API (<c>LogManager</c>, <c>NLog.Logger</c>) is used only in <c>Program.cs</c>.
/// Everything else logs through <c>ILogger&lt;T&gt;</c>.
/// </summary>
[TestFixture]
public sealed class NlogOnlyInProgramAnalyzerTests
{
    // A stand-in for the NLog package, so the snippets need no package reference.
    // The analyzer ignores types the compilation defines itself, so the stub's own code raises nothing.
    private const string NlogStub = """
        namespace NLog
        {
            public class Logger { public void Info(string message) { } }
            public interface ILogger { }
            public static class LogManager
            {
                public static Logger GetCurrentClassLogger() => new Logger();
                public static void Shutdown() { }
            }
        }
        namespace Microsoft.Extensions.Logging
        {
            public interface ILogger { }
            public interface ILogger<T> : ILogger { }
        }
        """;

    private static CSharpAnalyzerTest<NlogOnlyInProgramAnalyzer, DefaultVerifier> Create(string source, string fileName)
    {
        var test = new CSharpAnalyzerTest<NlogOnlyInProgramAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.Sources.Add((fileName, source));
        // The stub is a project of its own, like the real package
        test.TestState.AdditionalProjects["Stubs"].Sources.Add(("NLogStub.cs", NlogStub));
        test.TestState.AdditionalProjectReferences.Add("Stubs");
        return test;
    }

    private static Task VerifyAsync(string source, string fileName)
    {
        return Create(source, fileName).RunAsync();
    }

    private static Task VerifyAsync(CSharpAnalyzerTest<NlogOnlyInProgramAnalyzer, DefaultVerifier> test)
    {
        return test.RunAsync();
    }

    [Test]
    public Task LogManager_InService_ReportsTeam002()
    {
        const string source = """
            class Service
            {
                void Run() => NLog.{|TEAM002:LogManager|}.Shutdown();
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task LogManagerWithUsing_InService_ReportsTeam002()
    {
        const string source = """
            using NLog;
            class Service
            {
                void Run() => {|TEAM002:LogManager|}.Shutdown();
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task NlogLoggerField_InService_ReportsTeam002()
    {
        const string source = """
            class Service
            {
                private readonly NLog.{|TEAM002:Logger|} _logger = null!;
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task NlogILoggerParameter_InService_ReportsTeam002()
    {
        const string source = """
            class Service
            {
                void Run(NLog.{|TEAM002:ILogger|} logger) { }
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task MicrosoftNonGenericILogger_InService_ReportsNothing()
    {
        const string source = """
            using Microsoft.Extensions.Logging;
            class Service
            {
                void Run(ILogger logger) { }
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task LogManager_InProgram_ReportsNothing()
    {
        const string source = """
            using NLog;
            class Program
            {
                static void Main()
                {
                    try { LogManager.GetCurrentClassLogger().Info("start"); }
                    finally { LogManager.Shutdown(); }
                }
            }
            """;
        return VerifyAsync(source, "Program.cs");
    }

    [Test]
    public Task LogManager_InProgramInSubfolder_ReportsNothing()
    {
        const string source = """
            class Program
            {
                static void Main() => NLog.LogManager.Shutdown();
            }
            """;
        return VerifyAsync(source, "/repo/services/Members/Program.cs");
    }

    [Test]
    public Task MicrosoftLogger_InService_ReportsNothing()
    {
        const string source = """
            using Microsoft.Extensions.Logging;
            class Service
            {
                private readonly ILogger<Service> _logger;
                public Service(ILogger<Service> logger) => _logger = logger;
            }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task UsingNlogWithoutApiUse_InService_ReportsNothing()
    {
        const string source = """
            using NLog;
            class Service { }
            """;
        return VerifyAsync(source, "Service.cs");
    }

    [Test]
    public Task Message_FollowsFoundDoInForm()
    {
        const string source = """
            class Service
            {
                void Run() => NLog.{|#0:LogManager|}.Shutdown();
            }
            """;
        CSharpAnalyzerTest<NlogOnlyInProgramAnalyzer, DefaultVerifier> test = Create(source, "Service.cs");
        test.ExpectedDiagnostics.Add(new DiagnosticResult("TEAM002", DiagnosticSeverity.Error)
            .WithMessage("Found NLog 'LogManager' outside Program.cs. Do inject ILogger<T> and log through it, and keep the NLog setup in Program.cs.")
            .WithLocation(0));
        return VerifyAsync(test);
    }
}
