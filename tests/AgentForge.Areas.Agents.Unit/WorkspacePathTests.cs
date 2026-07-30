using AgentForge.Areas.Agents.Runtime.Workspace;

namespace AgentForge.Areas.Agents.Unit;

public class WorkspacePathTests
{
    [Fact]
    public void TryResolve_akzeptiert_relative_Pfade_unter_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-jail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.True(WorkspacePath.TryResolve(root, "src/a.txt", out var full, out var error));
            Assert.Null(error);
            Assert.StartsWith(Path.GetFullPath(root), full, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_lehnt_Parent_Escape_ab()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-jail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.False(WorkspacePath.TryResolve(root, "../secret.txt", out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
