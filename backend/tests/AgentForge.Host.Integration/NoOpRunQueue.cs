using System.Threading.Channels;
using AgentForge.Areas.Agents.Runtime.Queue;

namespace AgentForge.Host.Integration;

internal sealed class NoOpRunQueue : IRunQueue
{
    public void Enqueue(Guid runId)
    {
    }

    public async IAsyncEnumerable<Guid> ReadAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<Guid>();
        await foreach (var runId in channel.Reader.ReadAllAsync(ct))
        {
            yield return runId;
        }
    }
}
