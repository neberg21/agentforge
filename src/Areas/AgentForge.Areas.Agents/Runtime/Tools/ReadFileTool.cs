using System.Text;
using System.Text.Json;
using AgentForge.Areas.Agents.Runtime.Workspace;

namespace AgentForge.Areas.Agents.Runtime.Tools;

public sealed class ReadFileTool : ITool
{
    public string Name => "read_file";

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        var context = RunWorkspaceContext.Current;
        if (context is null)
        {
            return """{"ok":false,"error":"no_workspace"}""";
        }

        string? relative;
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            relative = document.RootElement.TryGetProperty("path", out var pathElement)
                ? pathElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return """{"ok":false,"error":"invalid_arguments"}""";
        }

        if (string.IsNullOrWhiteSpace(relative))
        {
            return """{"ok":false,"error":"path_required"}""";
        }

        if (!WorkspacePath.TryResolve(context.Root, relative, out var fullPath, out var error))
        {
            return $"{{\"ok\":false,\"error\":\"{Escape(error ?? "path_rejected")}\"}}";
        }

        if (!File.Exists(fullPath))
        {
            return """{"ok":false,"error":"file_not_found"}""";
        }

        var content = await File.ReadAllTextAsync(fullPath, ct);
        return JsonSerializer.Serialize(new { ok = true, content });
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
