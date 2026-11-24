using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM009: a controller action has an explicit HTTP-verb attribute and a typed result
/// (<c>ActionResult&lt;T&gt;</c> or a typed result from <c>Microsoft.AspNetCore.Http.HttpResults</c>).
/// </summary>
[TestFixture]
public sealed class ControllerActionContractAnalyzerTests
{
    // A stand-in for the ASP.NET Core shared framework, so the snippets need no framework reference.
    // It is a project of its own, like the real assemblies.
    private const string AspNetStub = """
        using System;
        namespace Microsoft.AspNetCore.Mvc
        {
            public interface IActionResult { }
            public class ActionResult : IActionResult { }
            public sealed class ActionResult<TValue> { public static implicit operator ActionResult<TValue>(TValue value) => new(); }
            public abstract class ControllerBase { }
            public abstract class Controller : ControllerBase { }
            public sealed class ApiControllerAttribute : Attribute { }
            public sealed class NonActionAttribute : Attribute { }
            public sealed class RouteAttribute : Attribute { public RouteAttribute(string template) { } }
            public sealed class HttpGetAttribute : Routing.HttpMethodAttribute { public HttpGetAttribute() { } public HttpGetAttribute(string template) { } }
            public sealed class HttpPostAttribute : Routing.HttpMethodAttribute { }
            public sealed class HttpDeleteAttribute : Routing.HttpMethodAttribute { }
        }
        namespace Microsoft.AspNetCore.Mvc.Routing
        {
            public abstract class HttpMethodAttribute : Attribute { }
        }
        namespace Microsoft.AspNetCore.Http
        {
            public interface IResult { }
        }
        namespace Microsoft.AspNetCore.Http.HttpResults
        {
            public sealed class Ok<TValue> : IResult { }
            public sealed class NotFound : IResult { }
            public sealed class NoContent : IResult { }
            public sealed class Results<TResult1, TResult2> : IResult { }
        }
        """;

    private const string Usings = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Http.HttpResults;
        using Microsoft.AspNetCore.Mvc;

        public class ClassSession { }

        """;

    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<ControllerActionContractAnalyzer, DefaultVerifier>
        {
            TestCode = Usings + source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalProjects["AspNet"].Sources.Add(("AspNetStub.cs", AspNetStub));
        test.TestState.AdditionalProjectReferences.Add("AspNet");
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    // --- raises TEAM009: no HTTP verb ---

    [Test]
    public Task ActionWithoutVerb_ReportsTeam009()
    {
        const string source = """
            [ApiController]
            public class ClassSessionsController : ControllerBase
            {
                public ActionResult<ClassSession> {|TEAM009:Get|}(string id) => new ClassSession();
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ActionWithOnlyRoute_ReportsTeam009()
    {
        const string source = """
            public class ClassSessionsController : Controller
            {
                [Route("{id}")]
                public ActionResult<ClassSession> {|TEAM009:Get|}(string id) => new ClassSession();
            }
            """;
        return VerifyAsync(source);
    }

    // --- raises TEAM009: untyped result ---

    [TestCase("IActionResult")]
    [TestCase("ActionResult")]
    [TestCase("IResult")]
    [TestCase("ClassSession")]
    [TestCase("List<ClassSession>")]
    [TestCase("Task<IActionResult>")]
    [TestCase("Task")]
    [TestCase("void")]
    public Task ActionWithUntypedResult_ReportsTeam009(string returnType)
    {
        string source = $$"""
            public class ClassSessionsController : ControllerBase
            {
                [HttpGet]
                public {{returnType}} {|TEAM009:Get|}() => throw null!;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NoVerbAndUntypedResult_ReportsBoth()
    {
        const string source = """
            public class ClassSessionsController : ControllerBase
            {
                public IActionResult {|TEAM009:{|TEAM009:Get|}|}() => throw null!;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task MissingVerb_MessageSaysWhatToDo()
    {
        const string source = """
            public class ClassSessionsController : ControllerBase
            {
                public ActionResult<ClassSession> Get() => new ClassSession();
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(ControllerActionContractAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(10, 39, 10, 42)
            .WithMessage("Found action 'Get' without an HTTP-verb attribute. Do add [HttpGet], [HttpPost], [HttpPut], [HttpPatch] or [HttpDelete] to it, in ClassSessionsController.");
        return VerifyAsync(source, expected);
    }

    [Test]
    public Task UntypedResult_MessageSaysWhatToDo()
    {
        const string source = """
            public class ClassSessionsController : ControllerBase
            {
                [HttpGet]
                public IActionResult Get() => throw null!;
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(ControllerActionContractAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(11, 26, 11, 29)
            .WithMessage("Found action 'Get' returning 'IActionResult'. Do return ActionResult<T> or a typed result (Ok<T>, NotFound, Results<...>) instead, in ClassSessionsController.");
        return VerifyAsync(source, expected);
    }

    // --- reports nothing ---

    [TestCase("ActionResult<ClassSession>")]
    [TestCase("Task<ActionResult<ClassSession>>")]
    [TestCase("ValueTask<ActionResult<List<ClassSession>>>")]
    [TestCase("Ok<ClassSession>")]
    [TestCase("NoContent")]
    [TestCase("Task<Results<Ok<ClassSession>, NotFound>>")]
    public Task ActionWithVerbAndTypedResult_ReportsNothing(string returnType)
    {
        string source = $$"""
            public class ClassSessionsController : ControllerBase
            {
                [HttpGet("{id}")]
                public {{returnType}} Get(string id) => throw null!;

                [HttpDelete]
                public NoContent Delete() => throw null!;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NonPublicStaticAndNonActionMethods_ReportNothing()
    {
        const string source = """
            public class ClassSessionsController : ControllerBase
            {
                private IActionResult Helper() => throw null!;
                protected void Log() { }
                public static IActionResult Create() => throw null!;

                [NonAction]
                public IActionResult NotAnAction() => throw null!;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ClassThatIsNotAController_ReportsNothing()
    {
        const string source = """
            public class ClassSessionService
            {
                public IActionResult Get() => throw null!;
            }
            """;
        return VerifyAsync(source);
    }
}
