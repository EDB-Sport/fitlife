using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM007: no <c>Thread.Sleep</c> and no <c>Task.Delay</c> in test projects (<c>*.Tests</c>, <c>*.IntegrationTests</c>).
/// Tests move time with <c>FakeTimeProvider</c> instead.
/// </summary>
[TestFixture]
public sealed class NoWaitInTestsAnalyzerTests
{
    // The snippet compiles as an assembly with this name, like a project under tests/ does
    private static Task VerifyAsync(string source, string assemblyName, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<NoWaitInTestsAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, assemblyName));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM007 ---

    [TestCase("BookingService.Tests")]
    [TestCase("BookingService.IntegrationTests")]
    public Task ThreadSleep_InTestProject_ReportsTeam007(string assemblyName)
    {
        const string source = """
            using System.Threading;

            class ReservationTests
            {
                void Waits()
                {
                    {|TEAM007:Thread.Sleep(100)|};
                }
            }
            """;
        return VerifyAsync(source, assemblyName);
    }

    [Test]
    public Task ThreadSleepWithTimeSpan_InTestProject_ReportsTeam007()
    {
        const string source = """
            using System;
            using System.Threading;

            class ReservationTests
            {
                void Waits()
                {
                    {|TEAM007:Thread.Sleep(TimeSpan.FromSeconds(1))|};
                }
            }
            """;
        return VerifyAsync(source, "BookingService.Tests");
    }

    [TestCase("Task.Delay(100)")]
    [TestCase("Task.Delay(TimeSpan.FromSeconds(1))")]
    [TestCase("Task.Delay(100, CancellationToken.None)")]
    public Task TaskDelay_InTestProject_ReportsTeam007(string call)
    {
        string source = $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            class ReservationTests
            {
                async Task Waits()
                {
                    await {|TEAM007:{{call}}|};
                }
            }
            """;
        return VerifyAsync(source, "BookingService.Tests");
    }

    [Test]
    public Task TaskDelay_MessageSaysWhatToDoInstead()
    {
        const string source = """
            using System.Threading.Tasks;

            class ReservationTests
            {
                async Task Waits()
                {
                    await Task.Delay(100);
                }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(NoWaitInTestsAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(7, 15, 7, 30)
            .WithMessage("Found Task.Delay in test project BookingService.Tests. Do advance a FakeTimeProvider (Microsoft.Extensions.Time.Testing) instead of waiting, in the test and in the code under test.");
        return VerifyAsync(source, "BookingService.Tests", expected);
    }

    // --- reports nothing ---

    [Test]
    public Task TaskDelay_InServiceProject_ReportsNothing()
    {
        const string source = """
            using System.Threading.Tasks;

            class RetryPolicy
            {
                async Task Waits()
                {
                    await Task.Delay(100);
                }
            }
            """;
        return VerifyAsync(source, "BookingService");
    }

    [Test]
    public Task TaskDelayWithTimeProvider_InTestProject_ReportsNothing()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            class ReservationTests
            {
                async Task Waits(TimeProvider time)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), time);
                    await Task.Delay(TimeSpan.FromSeconds(1), time, CancellationToken.None);
                }
            }
            """;
        return VerifyAsync(source, "BookingService.Tests");
    }

    [Test]
    public Task OwnMethodNamedSleep_InTestProject_ReportsNothing()
    {
        const string source = """
            class Thread
            {
                public static void Sleep(int milliseconds) { }
            }

            class ReservationTests
            {
                void Calls()
                {
                    Thread.Sleep(100);
                }
            }
            """;
        return VerifyAsync(source, "BookingService.Tests");
    }

    [Test]
    public Task ProjectNameOnlyContainingTests_ReportsNothing()
    {
        const string source = """
            using System.Threading;

            class Worker
            {
                void Waits()
                {
                    Thread.Sleep(100);
                }
            }
            """;
        return VerifyAsync(source, "BookingService.Tests.Helpers");
    }
}
