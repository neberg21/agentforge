using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed record ConversationTitleJob(Guid ConversationId);

public interface IConversationTitleQueue
{
    bool TryEnqueue(ConversationTitleJob job);

    void MarkCompleted(Guid conversationId);

    IAsyncEnumerable<ConversationTitleJob> ReadAllAsync(CancellationToken ct);
}

public sealed class ChannelConversationTitleQueue : IConversationTitleQueue
{
    private readonly Channel<ConversationTitleJob> _channel =
        Channel.CreateUnbounded<ConversationTitleJob>();

    private readonly ConcurrentDictionary<Guid, byte> _inflight = new();

    public bool TryEnqueue(ConversationTitleJob job)
    {
        if (!_inflight.TryAdd(job.ConversationId, 0))
        {
            return false;
        }

        if (!_channel.Writer.TryWrite(job))
        {
            _inflight.TryRemove(job.ConversationId, out _);
            return false;
        }

        return true;
    }

    public void MarkCompleted(Guid conversationId) =>
        _inflight.TryRemove(conversationId, out _);

    public IAsyncEnumerable<ConversationTitleJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
