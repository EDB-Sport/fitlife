namespace FitLife.Contracts.Tests;

/// <summary>
/// contracts/openapi/*.json in the working tree, as the service builds write them (#395).
/// A build on Windows must not turn a line break in a description into <c>\r\n</c>: CI writes <c>\n</c>, so the tree would be dirty after every local run.
/// </summary>
public sealed class OpenApiContractTests
{
    private const string EscapedCarriageReturn = @"\r";

    [TestCase("bookingservice")]
    [TestCase("identityservice")]
    [TestCase("membershipservice")]
    public void Contract_AsTheBuildWroteIt_HasNoCarriageReturns(string service)
    {
        // Arrange
        string path = Path.Combine(RepoRoot(), "contracts", "openapi", service + ".json");

        // Act
        string contract = File.ReadAllText(path);

        // Assert
        Assert.That(contract, Does.Not.Contain('\r').And.Not.Contain(EscapedCarriageReturn), $"{path} has a carriage return; contracts use line feeds only.");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FitLife.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No FitLife.slnx above " + AppContext.BaseDirectory);
    }
}
