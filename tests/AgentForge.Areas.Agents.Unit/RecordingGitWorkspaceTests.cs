namespace AgentForge.Areas.Agents.Unit;

public class RecordingGitWorkspaceTests
{
    [Fact]
    public async Task AddWorktreeAsync_WhenCalled_RecordsCall()
    {
        var git = new RecordingGitWorkspace();
        var worktree = Path.Combine(Path.GetTempPath(), "wt-" + Guid.NewGuid().ToString("N"));

        await git.AddWorktreeAsync(
            @"C:\repo",
            worktree,
            "run/demo",
            "main",
            TestContext.Current.CancellationToken);

        Assert.Contains(git.Calls, call => call.StartsWith("AddWorktree:", StringComparison.Ordinal));
        Assert.True(Directory.Exists(worktree));
        Directory.Delete(worktree, recursive: true);
    }
}
