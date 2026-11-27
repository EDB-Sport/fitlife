namespace McpServer.Guardrails;

/// <summary>The MCP server's settings, the <c>Mcp</c> section of the configuration.</summary>
public sealed class McpOptions
{
    /// <summary>The configuration section.</summary>
    public const string Section = "Mcp";

    /// <summary>At most this many requests to <c>/mcp</c> per account and minute (#216).</summary>
    public int CallsPerMinute { get; set; }
}
