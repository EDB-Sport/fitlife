using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM005: no <c>dynamic</c>, no <c>goto</c>, no LINQ query syntax, no nested conditional expressions,
/// and lambdas nested at most one level deep (a lambda inside a lambda is fine, a third level is not).
/// </summary>
[TestFixture]
public sealed class ReadabilityLimitsAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected)
    {
        return AnalyzerVerifier.VerifyAsync<ReadabilityLimitsAnalyzer>(source, analyzerConfigFile: null, TestBehaviors.None, expected);
    }

    // --- dynamic ---

    [Test]
    public Task DynamicParameter_ReportsTeam005()
    {
        const string source = """
            class C
            {
                object M({|TEAM005:dynamic|} value) => value;
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task DynamicLocal_ReportsTeam005()
    {
        const string source = """
            class C
            {
                void M()
                {
                    {|TEAM005:dynamic|} value = 1;
                    System.Console.WriteLine((object)value);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task TypeOfYourOwnNamedDynamic_ReportsNothing()
    {
        const string source = """
            class dynamic { }

            class C
            {
                void M(dynamic value) { }
            }
            """;
        return VerifyAsync(source);
    }

    // --- goto ---

    [Test]
    public Task Goto_ReportsTeam005()
    {
        const string source = """
            class C
            {
                void M()
                {
                    {|TEAM005:goto done;|}
                done:
                    return;
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task GotoCaseAndGotoDefault_ReportTeam005()
    {
        const string source = """
            class C
            {
                void M(int value)
                {
                    switch (value)
                    {
                        case 1:
                            {|TEAM005:goto case 2;|}
                        case 2:
                            {|TEAM005:goto default;|}
                        default:
                            break;
                    }
                }
            }
            """;
        return VerifyAsync(source);
    }

    // --- LINQ query syntax ---

    [Test]
    public Task QuerySyntax_ReportsTeam005()
    {
        const string source = """
            using System.Linq;

            class C
            {
                int[] M(int[] seats)
                {
                    return ({|TEAM005:from seat in seats where seat > 0 select seat|}).ToArray();
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task MethodSyntax_ReportsNothing()
    {
        const string source = """
            using System.Linq;

            class C
            {
                int[] M(int[] seats)
                {
                    return seats.Where(seat => seat > 0).ToArray();
                }
            }
            """;
        return VerifyAsync(source);
    }

    // --- nested conditional expressions ---

    [Test]
    public Task ConditionalInWhenFalse_ReportsTeam005()
    {
        const string source = """
            class C
            {
                void M(int value)
                {
                    System.Console.WriteLine(value > 0 ? "positive" : {|TEAM005:value < 0 ? "negative" : "zero"|});
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ParenthesizedConditionalInWhenTrue_ReportsTeam005()
    {
        const string source = """
            class C
            {
                void M(bool a, bool b)
                {
                    System.Console.WriteLine(a ? ({|TEAM005:b ? 1 : 2|}) : 3);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ConditionalInCondition_ReportsTeam005()
    {
        const string source = """
            class C
            {
                void M(bool a, bool b, bool c)
                {
                    System.Console.WriteLine(({|TEAM005:a ? b : c|}) ? 1 : 2);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task SingleConditional_ReportsNothing()
    {
        const string source = """
            class C
            {
                void M(int value)
                {
                    System.Console.WriteLine(value > 0 ? "positive" : "not positive");
                }
            }
            """;
        return VerifyAsync(source);
    }

    // --- lambda nesting ---

    [Test]
    public Task LambdaInsideLambda_ReportsNothing()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool M(List<int[]> rows)
                {
                    return rows.Any(row => row.Any(seat => seat > 0));
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ThirdLevelLambda_ReportsTeam005()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            class C
            {
                bool M(List<List<int[]>> halls)
                {
                    return halls.Any(hall => hall.Any(row => row.Any({|TEAM005:seat => seat > 0|})));
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ThirdLevelAnonymousMethod_ReportsTeam005()
    {
        const string source = """
            using System;

            class C
            {
                void M()
                {
                    Func<Func<Func<int>>> make = () => () => {|TEAM005:delegate { return 1; }|};
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task Goto_MessageSaysWhatToDo()
    {
        const string source = """
            class C
            {
                void M()
                {
                    goto done;
                done:
                    return;
                }
            }
            """;
        DiagnosticResult expected = new DiagnosticResult(ReadabilityLimitsAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(5, 9, 5, 19)
            .WithMessage("Found 'goto'. Do use a loop, an if statement or an early return instead, in C.M.");
        return VerifyAsync(source, expected);
    }
}
