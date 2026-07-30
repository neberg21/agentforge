namespace AgentForge.Areas.Agents.Runtime.Tools;

public interface ITool
{
    string Name { get; }

    Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct);
}

public interface IToolRegistry
{
    void Register(ITool tool);

    ITool? Find(string name);

    void EnsureStubs(IEnumerable<string> names);

    Task<string> ExecuteOrErrorAsync(string name, string argumentsJson, CancellationToken ct);
}

public sealed class StubTool : ITool
{
    public StubTool(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        var payload = $"{{\"ok\":true,\"tool\":\"{Name}\",\"note\":\"stub\"}}";
        return Task.FromResult(payload);
    }
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);

    public void Register(ITool tool) => _tools[tool.Name] = tool;

    public ITool? Find(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;

    public void EnsureStubs(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (!_tools.ContainsKey(name))
            {
                Register(new StubTool(name));
            }
        }
    }

    public async Task<string> ExecuteOrErrorAsync(string name, string argumentsJson, CancellationToken ct)
    {
        var tool = Find(name);
        if (tool is null)
        {
            return $"{{\"ok\":false,\"error\":\"unknown_tool\",\"tool\":\"{name}\"}}";
        }

        try
        {
            return await tool.ExecuteAsync(argumentsJson, ct);
        }
        catch (Exception ex)
        {
            return $"{{\"ok\":false,\"error\":\"tool_failed\",\"tool\":\"{name}\",\"message\":\"{ex.Message}\"}}";
        }
    }
}
