using AgentForge.Areas.Agents.Runtime.Workspace;

namespace AgentForge.Host.Integration;

public sealed class RecordingGitWorkspace : IGitWorkspace
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public Task EnsureCloneAsync(string remoteUrl, string localPath, CancellationToken ct)
    {
        _calls.Add($"EnsureClone:{remoteUrl}:{localPath}");
        Directory.CreateDirectory(localPath);
        Directory.CreateDirectory(Path.Combine(localPath, ".git"));
        return Task.CompletedTask;
    }

    public Task FetchAsync(string localPath, CancellationToken ct)
    {
        _calls.Add($"Fetch:{localPath}");
        return Task.CompletedTask;
    }

    public Task AddWorktreeAsync(
        string localPath,
        string worktreePath,
        string branchName,
        string baseRef,
        CancellationToken ct)
    {
        _calls.Add($"AddWorktree:{localPath}:{worktreePath}:{branchName}:{baseRef}");
        Directory.CreateDirectory(worktreePath);
        return Task.CompletedTask;
    }

    public Task RemoveWorktreeAsync(string localPath, string worktreePath, CancellationToken ct)
    {
        _calls.Add($"RemoveWorktree:{localPath}:{worktreePath}");
        if (Directory.Exists(worktreePath))
        {
            Directory.Delete(worktreePath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task PushBranchAsync(string worktreePath, string branchName, CancellationToken ct)
    {
        _calls.Add($"PushBranch:{worktreePath}:{branchName}");
        return Task.CompletedTask;
    }
}
