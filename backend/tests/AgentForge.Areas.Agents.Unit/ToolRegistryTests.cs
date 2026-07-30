using AgentForge.Areas.Agents.Runtime.Tools;

namespace AgentForge.Areas.Agents.Unit;

public class ToolRegistryTests
{
    [Fact]
    public async Task EnsureStubs_WhenNamesMissing_RegistersStubTools()
    {
        var registry = new ToolRegistry();
        registry.EnsureStubs(["read_file", "write_file"]);

        var result = await registry.ExecuteOrErrorAsync("read_file", "{}", TestContext.Current.CancellationToken);

        Assert.Contains("\"ok\":true", result, StringComparison.Ordinal);
        Assert.Contains("read_file", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteOrErrorAsync_WhenUnknown_ReturnsErrorJson()
    {
        var registry = new ToolRegistry();

        var result = await registry.ExecuteOrErrorAsync("missing", "{}", TestContext.Current.CancellationToken);

        Assert.Contains("unknown_tool", result, StringComparison.Ordinal);
    }
}
