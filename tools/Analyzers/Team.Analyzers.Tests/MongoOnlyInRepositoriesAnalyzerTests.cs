using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM001: <c>MongoDB.Driver</c> types are used only in a <c>*.Repositories</c> namespace.
/// </summary>
[TestFixture]
public sealed class MongoOnlyInRepositoriesAnalyzerTests
{
    // A stand-in for the MongoDB.Driver and MongoDB.Bson packages, so the snippets need no package reference.
    // It is a project of its own, like the real packages.
    private const string MongoStub = """
        namespace MongoDB.Driver
        {
            public interface IMongoClient { IMongoDatabase GetDatabase(string name); }
            public class MongoClient : IMongoClient
            {
                public MongoClient(string connectionString) { }
                public IMongoDatabase GetDatabase(string name) => null!;
            }
            public interface IMongoDatabase { IMongoCollection<T> GetCollection<T>(string name); }
            public interface IMongoCollection<T> { }
        }
        namespace MongoDB.Driver.Linq
        {
            public interface IMongoQueryable<T> { }
        }
        namespace MongoDB.Bson.Serialization.Attributes
        {
            public sealed class BsonIdAttribute : System.Attribute { }
        }
        """;

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        VerifyAsync(source, OutputKind.DynamicallyLinkedLibrary, expected);

    private static Task VerifyAsync(string source, OutputKind outputKind, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MongoOnlyInRepositoriesAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.OutputKind = outputKind;
        test.TestState.AdditionalProjects["Mongo"].Sources.Add(("MongoStub.cs", MongoStub));
        test.TestState.AdditionalProjectReferences.Add("Mongo");
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM001 ---

    [Test]
    public Task MongoTypesInController_ReportTeam001()
    {
        const string source = """
            using MongoDB.Driver;

            namespace BookingService.Controllers
            {
                public class ClassSession { }

                public class ClassSessionsController
                {
                    private readonly {|TEAM001:IMongoCollection<ClassSession>|} _sessions;

                    public ClassSessionsController({|TEAM001:IMongoDatabase|} database)
                    {
                        _sessions = database.GetCollection<ClassSession>("sessions");
                    }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task QualifiedNameAndObjectCreation_ReportTeam001()
    {
        const string source = """
            namespace BookingService.Services
            {
                public class Setup
                {
                    public object Create() => new MongoDB.Driver.{|TEAM001:MongoClient|}("mongodb://localhost");
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task SubNamespaceOfTheDriver_ReportsTeam001()
    {
        const string source = """
            using MongoDB.Driver.Linq;

            namespace BookingService.Services
            {
                public class Query
                {
                    public void Run({|TEAM001:IMongoQueryable<int>|} seats) { }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task TopLevelProgram_ReportsTeam001()
    {
        const string source = """
            using MongoDB.Driver;

            {|TEAM001:IMongoClient|} client = new {|TEAM001:MongoClient|}("mongodb://localhost");
            System.Console.WriteLine(client);
            """;
        return VerifyAsync(source, OutputKind.ConsoleApplication);
    }

    [Test]
    public Task NamespaceOnlyContainingRepositories_ReportsTeam001()
    {
        const string source = """
            using MongoDB.Driver;

            namespace BookingService.Repositories.Mapping
            {
                public class Mapper
                {
                    public void Map({|TEAM001:IMongoDatabase|} database) { }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task MongoTypeInController_MessageSaysWhatToDo()
    {
        const string source = """
            namespace BookingService.Controllers
            {
                public class ClassSessionsController
                {
                    public ClassSessionsController(MongoDB.Driver.IMongoDatabase database) { }
                }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(MongoOnlyInRepositoriesAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(5, 61, 5, 75)
            .WithMessage("Found MongoDB.Driver type 'IMongoDatabase' in BookingService.Controllers. Do the database work in a repository class in a *.Repositories namespace, and use its interface here.");
        return VerifyAsync(source, expected);
    }

    // --- reports nothing ---

    [Test]
    public Task MongoTypesInRepositories_ReportNothing()
    {
        const string source = """
            using MongoDB.Driver;

            namespace BookingService.Repositories
            {
                public class ClassSession { }

                public interface IClassSessionRepository { }

                public class ClassSessionRepository : IClassSessionRepository
                {
                    private readonly IMongoCollection<ClassSession> _sessions;

                    public ClassSessionRepository(IMongoClient client)
                    {
                        _sessions = client.GetDatabase("booking").GetCollection<ClassSession>("sessions");
                    }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task BsonAttributeOnModel_ReportsNothing()
    {
        const string source = """
            using MongoDB.Bson.Serialization.Attributes;

            namespace BookingService.Models
            {
                public class ClassSession
                {
                    [BsonId]
                    public string Id { get; set; } = "";
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ControllerUsingTheRepositoryInterface_ReportsNothing()
    {
        const string source = """
            namespace BookingService.Repositories
            {
                public interface IClassSessionRepository { }
            }

            namespace BookingService.Controllers
            {
                using BookingService.Repositories;

                public class ClassSessionsController
                {
                    public ClassSessionsController(IClassSessionRepository repository) { }
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task OwnTypeWithADriverName_ReportsNothing()
    {
        const string source = """
            namespace BookingService.Services
            {
                public class MongoClient { }

                public class Setup
                {
                    public MongoClient Create() => new MongoClient();
                }
            }
            """;
        return VerifyAsync(source);
    }
}
