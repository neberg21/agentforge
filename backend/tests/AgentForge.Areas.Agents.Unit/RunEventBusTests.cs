using AgentForge.Areas.Agents.Runtime.Events;

namespace AgentForge.Areas.Agents.Unit;

public class RunEventBusTests
{
    [Fact]
    public async Task Subscribe_WhenEventsPublished_ReceivesMatchingRunId()
    {
        var bus = new InProcessRunEventBus();
        var runId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();

        var received = new List<RunEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var reader = Task.Run(async () =>
        {
            await foreach (var ev in bus.Subscribe(runId, cts.Token))
            {
                received.Add(ev);
                if (ev.Type == RunEventType.Done)
                {
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50, cts.Token);

        bus.Publish(new RunEvent(otherId, RunEventType.Status, "{}"));
        bus.Publish(new RunEvent(runId, RunEventType.Status, "{\"status\":\"Running\"}"));
        bus.Publish(new RunEvent(runId, RunEventType.Done, "{}"));

        await reader;

        Assert.Equal(2, received.Count);
        Assert.Equal(RunEventType.Status, received[0].Type);
        Assert.Equal(RunEventType.Done, received[1].Type);
    }
}
