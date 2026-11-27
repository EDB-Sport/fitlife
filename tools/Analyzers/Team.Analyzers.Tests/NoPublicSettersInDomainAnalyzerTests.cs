using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM011: no public <c>set</c> accessor on a property of a type in a <c>*.Domain</c> namespace (METHODOLOGY 11.2 rule 4, #582).
/// State changes go through methods on the aggregate root. <c>init</c> and non-public setters are allowed.
/// </summary>
[TestFixture]
public sealed class NoPublicSettersInDomainAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        return AnalyzerVerifier.VerifyAsync<NoPublicSettersInDomainAnalyzer>(source, analyzerConfigFile: null, TestBehaviors.None, expected);
    }

    // --- raises TEAM011 ---

    [Test]
    public Task PublicAutoPropertySetter_ReportsTeam011()
    {
        const string source = """
            namespace BookingService.Domain;

            public sealed class ClassSession
            {
                public int Seats { get; {|TEAM011:set;|} }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PublicSetterWithABody_ReportsTeam011()
    {
        const string source = """
            namespace BookingService.Domain;

            public sealed class ClassSession
            {
                private int _seats;

                public int Seats
                {
                    get => _seats;
                    {|TEAM011:set => _seats = value;|}
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PublicSetterInANonPublicDomainType_ReportsTeam011()
    {
        const string source = """
            namespace MembershipService.Domain;

            internal sealed class Pause
            {
                public System.DateOnly From { get; {|TEAM011:set;|} }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PublicSetterInANestedDomainType_ReportsTeam011()
    {
        const string source = """
            namespace BookingService.Domain
            {
                public sealed class ClassSession
                {
                    public sealed class Seat
                    {
                        public int Number { get; {|TEAM011:set;|} }
                    }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task Message_SaysWhatToDo()
    {
        const string source = """
            namespace BookingService.Domain;

            public sealed class ClassSession
            {
                public int Seats { get; set; }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(NoPublicSettersInDomainAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(5, 29, 5, 33)
            .WithMessage(
                "Found a public setter on 'ClassSession.Seats' in BookingService.Domain. Do change the state through a method on the "
                + "aggregate root, and make the setter private (or drop it) (METHODOLOGY 11.2 rule 4).");
        return VerifyAsync(source, expected);
    }

    // --- doesn't raise TEAM011 ---

    [Test]
    public Task PrivateProtectedAndInternalSetters_AreAllowed()
    {
        const string source = """
            namespace BookingService.Domain;

            public class ClassSession
            {
                public int Seats { get; private set; }
                public int Version { get; internal set; }
                public bool IsCancelled { get; protected set; }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task InitAccessorsAndPositionalRecords_AreAllowed()
    {
        const string source = """
            namespace BookingService.Domain;

            public sealed record ReservationMade(System.Guid ClassSessionId, System.Guid MemberId);

            public sealed class Instructor
            {
                public string Name { get; init; } = string.Empty;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task GetOnlyProperty_IsAllowed()
    {
        const string source = """
            namespace BookingService.Domain;

            public sealed class ClassSession
            {
                public int Seats { get; }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PublicSetterOutsideADomainNamespace_IsAllowed()
    {
        const string source = """
            namespace BookingService.Models;

            public sealed class ClassSessionRequest
            {
                public int Seats { get; set; }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NamespaceThatOnlyStartsWithDomain_IsAllowed()
    {
        const string source = """
            namespace BookingService.Domain.Seed;

            public sealed class SeedRow
            {
                public int Seats { get; set; }
            }
            """;
        return VerifyAsync(source);
    }
}
