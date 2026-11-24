using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Team.Analyzers;

/// <summary>
/// TEAM009: a controller action has an explicit HTTP-verb attribute and a typed result
/// (<c>ActionResult&lt;T&gt;</c> or a typed result from <c>Microsoft.AspNetCore.Http.HttpResults</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControllerActionContractAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The ID this analyzer reports.</summary>
    public const string DiagnosticId = "TEAM009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Controller action without a verb or a typed result",
        messageFormat: "Found action '{0}' {1}. Do {2}, in {3}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated OpenAPI contract stays accurate only when every action names its HTTP verb and its result type, so the breaking-change check means something.",
        helpLinkUri: "https://github.com/EDB-Sport/fitlife/blob/develop/docs/CSHARP_SETUP_TODO.md#4-project-specific-roslyn-analyzers-toolsanalyzers");

    private const string VerbFix = "add [HttpGet], [HttpPost], [HttpPut], [HttpPatch] or [HttpDelete] to it";
    private const string ResultFix = "return ActionResult<T> or a typed result (Ok<T>, NotFound, Results<...>) instead";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            KnownTypes known = new(start.Compilation);
            if (known.ControllerBase is not null)
            {
                start.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, known), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, KnownTypes known)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!IsAction(method, known))
        {
            return;
        }

        Location location = method.Locations[0];
        string controller = method.ContainingType.Name;
        if (!method.GetAttributes().Any(attribute => InheritsFrom(attribute.AttributeClass, known.HttpMethodAttribute)))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name, "without an HTTP-verb attribute", VerbFix, controller));
        }

        if (!IsTypedResult(Unwrap(method.ReturnType, known), known))
        {
            string returned = $"returning '{method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'";
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name, returned, ResultFix, controller));
        }
    }

    // ASP.NET Core's own rule (DefaultApplicationModelProvider.IsAction): public, non-static, ordinary methods
    // of a controller, unless marked [NonAction], an override of an object method, or IDisposable.Dispose
    private static bool IsAction(IMethodSymbol method, KnownTypes known)
    {
        bool publicInstanceMethod = method is { MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, IsStatic: false };
        if (!publicInstanceMethod || !InheritsFrom(method.ContainingType, known.ControllerBase))
        {
            return false;
        }

        return !method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, known.NonActionAttribute))
            && !OverridesObjectMethod(method)
            && !ImplementsDispose(method, known);
    }

    // Follow the override chain to the first declaration
    private static bool OverridesObjectMethod(IMethodSymbol method)
    {
        IMethodSymbol current = method;
        while (current.OverriddenMethod is not null)
        {
            current = current.OverriddenMethod;
        }

        return current.ContainingType.SpecialType == SpecialType.System_Object;
    }

    private static bool ImplementsDispose(IMethodSymbol method, KnownTypes known)
    {
        if (known.DisposeMethod is null)
        {
            return false;
        }

        ISymbol? implementation = method.ContainingType.FindImplementationForInterfaceMember(known.DisposeMethod);
        return SymbolEqualityComparer.Default.Equals(implementation, method);
    }

    // Task<T> and ValueTask<T> are judged by their T
    private static ITypeSymbol Unwrap(ITypeSymbol type, KnownTypes known)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named
            && (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.TaskOfT)
                || SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.ValueTaskOfT)))
        {
            return named.TypeArguments[0];
        }

        return type;
    }

    private static bool IsTypedResult(ITypeSymbol type, KnownTypes known)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, known.ActionResultOfT))
        {
            return true;
        }

        return string.Equals(type.ContainingNamespace?.ToDisplayString(), "Microsoft.AspNetCore.Http.HttpResults", StringComparison.Ordinal);
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, INamedTypeSymbol? baseType)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class KnownTypes(Compilation compilation)
    {
        public INamedTypeSymbol? ControllerBase { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");

        public INamedTypeSymbol? NonActionAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");

        public INamedTypeSymbol? HttpMethodAttribute { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute");

        public INamedTypeSymbol? ActionResultOfT { get; } = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ActionResult`1");

        public INamedTypeSymbol? TaskOfT { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

        public INamedTypeSymbol? ValueTaskOfT { get; } = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        public IMethodSymbol? DisposeMethod { get; } = compilation.GetSpecialType(SpecialType.System_IDisposable).GetMembers(nameof(IDisposable.Dispose)).OfType<IMethodSymbol>().FirstOrDefault();
    }
}
