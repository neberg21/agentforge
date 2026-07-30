using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentForge.Areas.Agents.Runtime.Events;

namespace AgentForge.Areas.Agents.Runtime.Events;

public sealed record ConversationEvent(Guid ConversationId, RunEventType Type, string JsonPayload);

public interface IConversationEventBus
{
    void Publish(ConversationEvent ev);

    IAsyncEnumerable<ConversationEvent> Subscribe(Guid conversationId, CancellationToken ct);
}

public sealed class InProcessConversationEventBus : IConversationEventBus
{
    private readonly ConcurrentDictionary<Guid, List<Channel<ConversationEvent>>> _subscribers = new();

    public void Publish(ConversationEvent ev)
    {
        if (!_subscribers.TryGetValue(ev.ConversationId, out var channels))
        {
            return;
        }

        List<Channel<ConversationEvent>> snapshot;
        lock (channels)
        {
            snapshot = channels.ToList();
        }

        foreach (var channel in snapshot)
        {
            channel.Writer.TryWrite(ev);
            if (ev.Type == RunEventType.Done)
            {
                channel.Writer.TryComplete();
            }
        }
    }

    public async IAsyncEnumerable<ConversationEvent> Subscribe(
        Guid conversationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<ConversationEvent>();
        var list = _subscribers.GetOrAdd(conversationId, _ => []);
        lock (list)
        {
            list.Add(channel);
        }

        try
        {
            await foreach (var ev in channel.Reader.ReadAllAsync(ct))
            {
                yield return ev;
            }
        }
        finally
        {
            lock (list)
            {
                list.Remove(channel);
            }
        }
    }
}
