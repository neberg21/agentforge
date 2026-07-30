using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AgentForge.Areas.Agents.Runtime.Events;

public enum RunEventType
{
    Status,
    Message,
    Usage,
    Error,
    Done
}

public sealed record RunEvent(Guid RunId, RunEventType Type, string JsonPayload);

public interface IRunEventBus
{
    void Publish(RunEvent ev);

    IAsyncEnumerable<RunEvent> Subscribe(Guid runId, CancellationToken ct);
}

public sealed class InProcessRunEventBus : IRunEventBus
{
    private readonly ConcurrentDictionary<Guid, List<Channel<RunEvent>>> _subscribers = new();

    public void Publish(RunEvent ev)
    {
        if (!_subscribers.TryGetValue(ev.RunId, out var channels))
        {
            return;
        }

        List<Channel<RunEvent>> snapshot;
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

    public async IAsyncEnumerable<RunEvent> Subscribe(
        Guid runId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<RunEvent>();
        var list = _subscribers.GetOrAdd(runId, _ => []);
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
