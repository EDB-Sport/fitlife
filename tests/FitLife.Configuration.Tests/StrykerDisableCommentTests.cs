namespace FitLife.Configuration.Tests;

/// <summary>Every ConfigureAwait(false) in src/ and libs/ has a Stryker disable comment: the false -> true mutant is equivalent and no test can kill it (#270, #322).</summary>
public sealed class StrykerDisableCommentTests
{
    private const string Call = "ConfigureAwait(false)";
    private const string Disable = "// Stryker disable once Boolean";
    private static readonly string[] Folders = ["src", "libs"];

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FitLife.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("FitLife.slnx not found above " + AppContext.BaseDirectory);
    }

    [Test]
    public void SourceFiles_PutAStrykerDisableCommentAboveEveryConfigureAwaitFalse()
    {
        // Arrange
        string root = RepositoryRoot();
        char separator = Path.DirectorySeparatorChar;
        IEnumerable<string> files = Folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{separator}obj{separator}", StringComparison.Ordinal) && !file.Contains($"{separator}bin{separator}", StringComparison.Ordinal));

        // Act
        List<string> uncommented = [];
        foreach (string file in files)
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(Call, StringComparison.Ordinal)
                    && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)
                    && (i == 0 || !lines[i - 1].TrimStart().StartsWith(Disable, StringComparison.Ordinal)))
                {
                    uncommented.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }
        }

        // Assert
        Assert.That(uncommented, Is.Empty, "Add '" + Disable + "' on the line above each call.");
    }
}
