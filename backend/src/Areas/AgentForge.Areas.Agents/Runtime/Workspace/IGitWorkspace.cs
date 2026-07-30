namespace AgentForge.Areas.Agents.Runtime.Workspace;

public interface IGitWorkspace
{
    Task EnsureCloneAsync(string remoteUrl, string localPath, CancellationToken ct);

    Task FetchAsync(string localPath, CancellationToken ct);

    Task AddWorktreeAsync(
        string localPath,
        string worktreePath,
        string branchName,
        string baseRef,
        CancellationToken ct);

    Task RemoveWorktreeAsync(string localPath, string worktreePath, CancellationToken ct);

    Task PushBranchAsync(string worktreePath, string branchName, CancellationToken ct);
}
