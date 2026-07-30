using System.Threading.Channels;

namespace AgentForge.Areas.Agents.Runtime.Queue;

public sealed record ConversationReplyJob(
    Guid ConversationId,
    Guid StreamId,
    IReadOnlyList<Guid> AgentIds);

public interface IConversationReplyQueue
{
    void Enqueue(ConversationReplyJob job);

    IAsyncEnumerable<ConversationReplyJob> ReadAllAsync(CancellationToken ct);
}

public sealed class ChannelConversationReplyQueue : IConversationReplyQueue
{
    private readonly Channel<ConversationReplyJob> _channel =
        Channel.CreateUnbounded<ConversationReplyJob>();

    public void Enqueue(ConversationReplyJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<ConversationReplyJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
