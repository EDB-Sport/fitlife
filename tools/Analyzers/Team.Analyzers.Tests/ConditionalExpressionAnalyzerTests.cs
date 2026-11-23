using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Team.Analyzers.Tests;

/// <summary>
/// TEAM012: a conditional expression (<c>?:</c>) is not used as a return value, an assigned value or a variable initializer.
/// Code writes <c>if</c>/<c>return</c> or <c>if</c>/<c>else</c> with an assignment in each branch instead.
/// </summary>
[TestFixture]
public sealed class ConditionalExpressionAnalyzerTests
{
    private static Task VerifyAsync(string source, params DiagnosticResult[] expected) =>
        AnalyzerVerifier.VerifyAsync<ConditionalExpressionAnalyzer>(source, analyzerConfigFile: null, TestBehaviors.None, expected);

    // --- raises TEAM012 ---

    [Test]
    public Task ReturnValue_ReportsTeam012()
    {
        const string source = """
            class C
            {
                int Max(int left, int right)
                {
                    return {|TEAM012:left > right ? left : right|};
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ParenthesizedReturnValue_ReportsTeam012()
    {
        const string source = """
            class C
            {
                int Max(int left, int right)
                {
                    return ({|TEAM012:left > right ? left : right|});
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task LocalVariableInitializer_ReportsTeam012()
    {
        const string source = """
            class C
            {
                void M(bool member)
                {
                    string label = {|TEAM012:member ? "member" : "guest"|};
                    System.Console.WriteLine(label);
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task FieldInitializer_ReportsTeam012()
    {
        const string source = """
            class C
            {
                private static readonly int _seats = {|TEAM012:System.Environment.Is64BitProcess ? 20 : 10|};
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task Assignment_ReportsTeam012()
    {
        const string source = """
            class C
            {
                private int _seats;

                void M(bool large)
                {
                    _seats = {|TEAM012:large ? 20 : 10|};
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task CompoundAssignment_ReportsTeam012()
    {
        const string source = """
            class C
            {
                int M(bool large)
                {
                    int seats = 10;
                    seats += {|TEAM012:large ? 10 : 0|};
                    return seats;
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task NestedConditional_ReportsOnlyTheOuterOne()
    {
        const string source = """
            class C
            {
                int Sign(int value)
                {
                    return {|TEAM012:value > 0 ? 1 : value < 0 ? -1 : 0|};
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ReturnValue_MessageSaysWhatToWriteInstead()
    {
        const string source = """
            class C
            {
                int Max(int left, int right)
                {
                    return left > right ? left : right;
                }
            }
            """;
        var expected = new DiagnosticResult(ConditionalExpressionAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithSpan(5, 16, 5, 42)
            .WithMessage("Found a conditional expression (?:) as a return value. Do write an if statement with a return in each branch in the method instead.");
        return VerifyAsync(source, expected);
    }

    // --- reports nothing ---

    [Test]
    public Task IfAndReturn_ReportsNothing()
    {
        const string source = """
            class C
            {
                int Max(int left, int right)
                {
                    if (left > right)
                    {
                        return left;
                    }

                    return right;
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ConditionalAsArgument_ReportsNothing()
    {
        const string source = """
            class C
            {
                void M(bool member)
                {
                    System.Console.WriteLine(member ? "member" : "guest");
                }
            }
            """;
        return VerifyAsync(source);
    }

    [Test]
    public Task ConditionalInsideInterpolatedString_ReportsNothing()
    {
        const string source = """
            class C
            {
                string M(int seats)
                {
                    string text = $"{seats} {(seats == 1 ? "seat" : "seats")}";
                    return text;
                }
            }
            """;
        return VerifyAsync(source);
    }
}
