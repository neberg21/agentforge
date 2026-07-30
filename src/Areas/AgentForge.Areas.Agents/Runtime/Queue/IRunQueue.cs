namespace AgentForge.Areas.Agents.Runtime.Queue;

public interface IRunQueue
{
    void Enqueue(Guid runId);

    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
}
