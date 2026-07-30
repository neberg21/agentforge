namespace AgentForge.Areas.Agents.Runtime.Workspace;

public sealed class RunWorkspaceContext
{
    private static readonly AsyncLocal<RunWorkspaceContext?> CurrentLocal = new();

    public RunWorkspaceContext(Guid runId, string root, string branchName)
    {
        RunId = runId;
        Root = root;
        BranchName = branchName;
    }

    public static RunWorkspaceContext? Current
    {
        get => CurrentLocal.Value;
        set => CurrentLocal.Value = value;
    }

    public Guid RunId { get; }

    public string Root { get; }

    public string BranchName { get; }
}
