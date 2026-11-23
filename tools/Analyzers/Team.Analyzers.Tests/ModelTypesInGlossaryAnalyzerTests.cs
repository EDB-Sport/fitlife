using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM008: every public type in a <c>*.Models</c> namespace is in the "In code" column of <c>GLOSSARY.md</c>,
/// which the build passes to the analyzer as an AdditionalFile.
/// </summary>
[TestFixture]
public sealed class ModelTypesInGlossaryAnalyzerTests
{
    // The shape of GLOSSARY.md on develop: the Terms table, then a second table whose column is "Names in code"
    private const string Glossary = """
        # Glossary

        ## Terms

        | Term | Meaning | In code | schema.org | Context | Kind | Avoid |
        |---|---|---|---|---|---|---|
        | Class session | One scheduled run of a class | `ClassSession` | Event | Booking | Aggregate root | Session, Class |
        | Reservation | A member's seat in one class session | `Reservation`, `ReservationStatus` | Reservation | Booking | Entity | Booking |

        ## Words that mean different things in different contexts

        | Word | Meaning in context A | Meaning in context B | Names in code |
        |---|---|---|---|
        | Member | A paying customer | A person in a class | `Customer` |
        """;

    private static Task VerifyAsync(string source, string? glossary = Glossary, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ModelTypesInGlossaryAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        if (glossary is not null)
        {
            test.TestState.AdditionalFiles.Add(("/repo/GLOSSARY.md", glossary));
        }

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM008 ---

    [Test]
    public Task PublicModelNotInGlossary_ReportsTeam008()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class {|TEAM008:Booking|} { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task PublicRecordEnumAndStructNotInGlossary_ReportTeam008()
    {
        const string source = """
            namespace BookingService.Models
            {
                public record {|TEAM008:Seat|}(int Number);
                public enum {|TEAM008:Status|} { Open }
                public struct {|TEAM008:Slot|} { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NameOnlyInTheOtherTable_ReportsTeam008()
    {
        const string source = """
            namespace MemberService.Models
            {
                public class {|TEAM008:Customer|} { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NameOnlyInTheAvoidColumn_ReportsTeam008()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class {|TEAM008:Session|} { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NotInGlossary_MessageSaysWhatToDo()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class Booking { }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(ModelTypesInGlossaryAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(3, 18, 3, 25)
            .WithMessage("Found model type 'Booking', which is not in the 'In code' column of GLOSSARY.md. Do rename it to the glossary's name for the concept, or add a row for it to GLOSSARY.md, with its schema.org type, in the same PR.");
        return VerifyAsync(source, Glossary, expected);
    }

    [Test]
    public Task NoGlossaryPassedIn_ReportsTeam008WithThatMessage()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class Reservation { }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(ModelTypesInGlossaryAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(3, 18, 3, 29)
            .WithMessage("Found model type 'Reservation', but GLOSSARY.md is not an AdditionalFile of this project. Do add <AdditionalFiles Include=\"GLOSSARY.md\" /> (the repo root's) in the root Directory.Build.props.");
        return VerifyAsync(source, glossary: null, expected);
    }

    // --- reports nothing ---

    [Test]
    public Task PublicModelsInGlossary_ReportNothing()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class ClassSession { }
                public class Reservation { }
                public enum ReservationStatus { Active }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task GenericModelInGlossary_ReportsNothing()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class Reservation<TId> { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task InternalOrNestedModel_ReportsNothing()
    {
        const string source = """
            namespace BookingService.Models
            {
                internal class Booking { }
                public class ClassSession
                {
                    public class Booking { }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [TestCase("BookingService.Controllers")]
    [TestCase("BookingService.Models.Internal")]
    [TestCase("BookingService.ViewModels")]
    public Task PublicTypeOutsideModelsNamespace_ReportsNothing(string ns)
    {
        var source = $$"""
            namespace {{ns}}
            {
                public class Booking { }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task OtherAdditionalFile_IsNotReadAsTheGlossary()
    {
        const string source = """
            namespace BookingService.Models
            {
                public class {|TEAM008:Booking|} { }
            }
            """;
        return VerifyWithFilesAsync(source, ("/repo/GLOSSARY.md", Glossary), ("/repo/docs/OTHER.md", "| In code |\n|---|\n| `Booking` |\n"));
    }

    private static Task VerifyWithFilesAsync(string source, params (string Path, string Content)[] additionalFiles)
    {
        var test = new CSharpAnalyzerTest<ModelTypesInGlossaryAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalFiles.AddRange(additionalFiles.Select(file => (file.Path, Microsoft.CodeAnalysis.Text.SourceText.From(file.Content))));
        return test.RunAsync();
    }
}
