using System.Text.Json;
using AgentForge.Areas.Agents.Runtime.Workspace;

namespace AgentForge.Areas.Agents.Runtime.Tools;

public sealed class WriteFileTool : ITool
{
    public string Name => "write_file";

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        var context = RunWorkspaceContext.Current;
        if (context is null)
        {
            return """{"ok":false,"error":"no_workspace"}""";
        }

        string? relative;
        string? content;
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            relative = document.RootElement.TryGetProperty("path", out var pathElement)
                ? pathElement.GetString()
                : null;
            content = document.RootElement.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString()
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

        if (content is null)
        {
            return """{"ok":false,"error":"content_required"}""";
        }

        if (!WorkspacePath.TryResolve(context.Root, relative, out var fullPath, out var error))
        {
            return $"{{\"ok\":false,\"error\":\"{Escape(error ?? "path_rejected")}\"}}";
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, ct);
        return """{"ok":true}""";
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
