namespace AgentForge.Areas.Agents.Runtime.Workspace;

public interface IRunWorkspaceSession
{
    Task<bool> BeginAsync(Guid runId, CancellationToken ct);

    void Bind();

    void Unbind();

    Task FinishAsync(Guid runId, CancellationToken ct);
}
