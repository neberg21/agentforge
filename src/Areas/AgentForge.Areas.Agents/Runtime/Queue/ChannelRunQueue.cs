using System.Threading.Channels;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed class ChannelRunQueue : IRunQueue
{
    private readonly Channel<Guid> _channel;

    public ChannelRunQueue()
    {
        _channel = Channel.CreateUnbounded<Guid>();
    }

    public void Enqueue(Guid runId) => _channel.Writer.TryWrite(runId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
