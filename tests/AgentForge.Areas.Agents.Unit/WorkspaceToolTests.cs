using System.Text.Json;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Tools;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class WorkspaceToolTests
{
    [Fact]
    public async Task ReadFile_and_WriteFile_roundtrip_under_context_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var context = new RunWorkspaceContext(Guid.CreateVersion7(), root, "run/demo");
            RunWorkspaceContext.Current = context;

            var write = new WriteFileTool();
            var writeResult = await write.ExecuteAsync(
                """{"path":"notes/hello.txt","content":"hi"}""",
                TestContext.Current.CancellationToken);
            Assert.Contains("\"ok\":true", writeResult, StringComparison.Ordinal);

            var read = new ReadFileTool();
            var readResult = await read.ExecuteAsync(
                """{"path":"notes/hello.txt"}""",
                TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(readResult);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal("hi", document.RootElement.GetProperty("content").GetString());
        }
        finally
        {
            RunWorkspaceContext.Current = null;
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFile_without_context_returns_no_workspace()
    {
        RunWorkspaceContext.Current = null;
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync("""{"path":"a.txt"}""", TestContext.Current.CancellationToken);
        Assert.Contains("no_workspace", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunShell_uses_worktree_cwd()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var context = new RunWorkspaceContext(Guid.CreateVersion7(), root, "run/demo");
            RunWorkspaceContext.Current = context;

            var options = Options.Create(new AgentsOptions
            {
                Workspace =
                {
                    ShellTimeout = TimeSpan.FromSeconds(30),
                    MaxOutputChars = 65_536
                },
                Pricing =
                {
                    PromptTokenPerMillion = 1m,
                    CompletionTokenPerMillion = 2m
                }
            });
            var tool = new RunShellTool(options);
            var result = await tool.ExecuteAsync(
                """{"command":"echo workspace-ok"}""",
                TestContext.Current.CancellationToken);

            using var document = JsonDocument.Parse(result);
            Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
            Assert.Equal(0, document.RootElement.GetProperty("exitCode").GetInt32());
            Assert.Contains("workspace-ok", document.RootElement.GetProperty("stdout").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            RunWorkspaceContext.Current = null;
            Directory.Delete(root, recursive: true);
        }
    }
}
