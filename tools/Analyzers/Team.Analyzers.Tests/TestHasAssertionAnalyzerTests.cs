using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM006: every NUnit test (<c>[Test]</c>, <c>[TestCase]</c>, <c>[TestCaseSource]</c>) contains at least one assertion
/// or a Moq <c>Verify(...)</c>.
/// </summary>
[TestFixture]
public sealed class TestHasAssertionAnalyzerTests
{
    // Stand-ins for NUnit and Moq, so the snippets need no package reference.
    // Each is a project of its own, like the real packages.
    private const string NunitStub = """
        using System;
        using System.Threading.Tasks;
        namespace NUnit.Framework
        {
            public sealed class TestFixtureAttribute : Attribute { }
            public sealed class TestAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
            public sealed class TestCaseAttribute : Attribute { public TestCaseAttribute(params object[] arguments) { } }
            public sealed class TestCaseSourceAttribute : Attribute { public TestCaseSourceAttribute(string sourceName) { } }
            public sealed class SetUpAttribute : Attribute { }
            public static class Is { public static object EqualTo(object expected) => expected; }
            public static class Assert
            {
                public static void That(object actual, object constraint) { }
                public static void That(bool condition) { }
                public static void Multiple(Action body) { }
                public static Task ThrowsAsync<T>(Func<Task> code) => Task.CompletedTask;
                public static void Fail(string message) { }
                public static void Pass() { }
                public static void Ignore(string reason) { }
                public static void Inconclusive(string reason) { }
                public static void Warn(string message) { }
            }
            public static class StringAssert { public static void Contains(string expected, string actual) { } }
        }
        namespace NUnit.Framework.Legacy
        {
            public static class ClassicAssert { public static void AreEqual(object expected, object actual) { } }
        }
        """;

    private const string MoqStub = """
        using System;
        using System.Linq.Expressions;
        namespace Moq
        {
            public class Mock { public void VerifyAll() { } }
            public class Mock<T> : Mock where T : class
            {
                public T Object => null!;
                public void Verify(Expression<Action<T>> expression) { }
                public void VerifyNoOtherCalls() { }
            }
        }
        """;

    private const string Usings = """
        using System;
        using System.Threading.Tasks;
        using Moq;
        using NUnit.Framework;
        using NUnit.Framework.Legacy;

        public interface IClassSessionRepository { void Save(); }

        """;

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TestHasAssertionAnalyzer, DefaultVerifier>
        {
            TestCode = Usings + source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalProjects["NUnit"].Sources.Add(("NunitStub.cs", NunitStub));
        test.TestState.AdditionalProjects["Moq"].Sources.Add(("MoqStub.cs", MoqStub));
        test.TestState.AdditionalProjectReferences.Add("NUnit");
        test.TestState.AdditionalProjectReferences.Add("Moq");
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM006 ---

    [Test]
    public Task TestWithoutAssertion_ReportsTeam006()
    {
        const string source = """
            [TestFixture]
            public class ReservationTests
            {
                [Test]
                public void {|TEAM006:Reserve_FreeSeat_Works|}()
                {
                    int seats = 1 + 1;
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task TestCaseAndTestCaseSourceWithoutAssertion_ReportTeam006()
    {
        const string source = """
            public class ReservationTests
            {
                private static readonly int[] Seats = [1, 2];

                [TestCase(1)]
                [TestCase(2)]
                public void {|TEAM006:Reserve|}(int seats) { }

                [TestCaseSource(nameof(Seats))]
                public async Task {|TEAM006:ReserveAsync|}(int seats) => await Task.Yield();
            }
            """;
        return VerifyAsync(source);
    }

    [TestCase("Assert.Pass();")]
    [TestCase("Assert.Ignore(\"later\");")]
    [TestCase("Assert.Inconclusive(\"unknown\");")]
    [TestCase("Assert.Warn(\"slow\");")]
    public Task TestWithOnlyANonFailingAssertCall_ReportsTeam006(string call)
    {
        var source = $$"""
            public class ReservationTests
            {
                [Test]
                public void {|TEAM006:Reserve|}()
                {
                    {{call}}
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task TestWithMockButNoVerify_ReportsTeam006()
    {
        const string source = """
            public class ReservationTests
            {
                [Test]
                public void {|TEAM006:Reserve_SavesTheSession|}()
                {
                    var repository = new Mock<IClassSessionRepository>();
                    repository.Object.Save();
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task TestWithoutAssertion_MessageSaysWhatToDo()
    {
        const string source = """
            public class ReservationTests
            {
                [Test]
                public void Reserve() { }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(TestHasAssertionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(11, 17, 11, 24)
            .WithMessage("Found test 'Reserve' without an assertion. Do add an Assert.That(...) (or a Moq Verify) that fails when the behaviour is wrong, in ReservationTests.");
        return VerifyAsync(source, expected);
    }

    // --- reports nothing ---

    [TestCase("Assert.That(1 + 1, Is.EqualTo(2));")]
    [TestCase("Assert.That(true);")]
    [TestCase("Assert.Fail(\"not reached\");")]
    [TestCase("ClassicAssert.AreEqual(2, 1 + 1);")]
    [TestCase("StringAssert.Contains(\"a\", \"abc\");")]
    [TestCase("Assert.Multiple(() => { Assert.That(true); });")]
    public Task TestWithAssertion_ReportsNothing(string assertion)
    {
        var source = $$"""
            public class ReservationTests
            {
                [Test]
                public void Reserve()
                {
                    {{assertion}}
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task AsyncTestWithThrowsAsync_ReportsNothing()
    {
        const string source = """
            public class ReservationTests
            {
                [Test]
                public Task Reserve_FullSession_Throws() =>
                    Assert.ThrowsAsync<InvalidOperationException>(() => Task.CompletedTask);
            }
            """;
        return VerifyAsync(source);
    }

    [TestCase("repository.Verify(r => r.Save());")]
    [TestCase("repository.VerifyNoOtherCalls();")]
    [TestCase("repository.VerifyAll();")]
    public Task TestWithMoqVerify_ReportsNothing(string verify)
    {
        var source = $$"""
            public class ReservationTests
            {
                [Test]
                public void Reserve_SavesTheSession()
                {
                    var repository = new Mock<IClassSessionRepository>();
                    repository.Object.Save();
                    {{verify}}
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task MethodsThatAreNotTests_ReportNothing()
    {
        const string source = """
            public class ReservationTests
            {
                [SetUp]
                public void CreateSession() { }

                private void Helper() { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task OwnTypeNamedAssert_DoesNotCount()
    {
        const string source = """
            public class ReservationTests
            {
                private static class Assert { public static void That(bool condition) { } }

                [Test]
                public void {|TEAM006:Reserve|}()
                {
                    Assert.That(true);
                }
            }
            """;
        return VerifyAsync(source);
    }
}
