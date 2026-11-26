using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;

namespace McpServer.IntegrationTests;

/// <summary>
/// The tool list is a contract (#214): <c>contracts/mcp/tools.json</c> lists every tool's name, description and input names.
/// <c>tools/ci/check-contracts.sh</c> only watches <c>contracts/openapi/</c>, so this test is the only guard of <c>contracts/mcp/</c>.
/// </summary>
public sealed class ToolListContractTests
{
    [Test]
    public async Task ToolList_FromARunningServer_MatchesTheCommittedContract()
    {
        // Arrange
        using var factory = new McpFactory();
        using HttpClient http = factory.CreateClient();
        await using McpClient client = await McpFactory.ConnectAsync(http).ConfigureAwait(false);
        string path = Path.Combine(RepoRoot(), "contracts", "mcp", "tools.json");

        // Act
        IList<McpClientTool> tools = await client.ListToolsAsync().ConfigureAwait(false);

        // Assert
        JsonNode running = new JsonObject
        {
            ["tools"] = new JsonArray([.. tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(Describe)]),
        };
        JsonNode? committed = JsonNode.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
        Assert.That(
            JsonNode.DeepEquals(running, committed),
            Is.True,
            $"The tool list of the running server differs from {path}.{Environment.NewLine}Running: {running.ToJsonString()}{Environment.NewLine}Committed: {committed?.ToJsonString()}");
    }

    private static JsonObject Describe(McpClientTool tool)
    {
        IEnumerable<string> inputs = tool.ProtocolTool.InputSchema.TryGetProperty("properties", out JsonElement properties)
            ? properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            : [];
        return new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputs"] = new JsonArray([.. inputs.Select(name => JsonValue.Create(name))]),
        };
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
