using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Queue;

namespace AgentForge.Areas.Agents.Unit;

public class RunServiceTests
{
    private static AgentDefinition Definition(string name = "Builder") =>
        new(name, null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []);

    private sealed class RecordingRunQueue : IRunQueue
    {
        public List<Guid> Enqueued { get; } = [];

        public void Enqueue(Guid runId) => Enqueued.Add(runId);

        public async IAsyncEnumerable<Guid> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed record Fixture(
        AgentsDbContext Context,
        AgentService Agents,
        RunService Runs,
        TestClock Clock,
        RecordingRunQueue Queue) : IDisposable
    {
        public void Dispose() => Context.Dispose();
    }

    private static Fixture NewFixture(AgentsDatabase database)
    {
        var clock = TestClock.AtEpoch();
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var queue = new RecordingRunQueue();
        var runs = new RunService(context, clock, queue);
        return new Fixture(context, agents, runs, clock, queue);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentMissing_ReturnsNotFound()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.CreateAsync(Guid.CreateVersion7(), "Los.", TestContext.Current.CancellationToken);

        Assert.Equal("agent_not_found", result.Error!.Value.Code);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentArchived_ReturnsConflict()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        await fixture.Agents.ArchiveAsync(agent.Value!.Id, TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CreateAsync(agent.Value.Id, "Los.", TestContext.Current.CancellationToken);

        Assert.Equal(ErrorKind.Conflict, result.Error!.Value.Kind);
        Assert.Equal("agent_archived", result.Error!.Value.Code);
        Assert.Empty(fixture.Queue.Enqueued);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentActive_CreatesPendingRunWithTwoMessages()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CreateAsync(agent.Value!.Id, "Baue eine Todo-App.", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RunStatus.Pending, result.Value!.Status);
        Assert.Equal("Du bist hilfreich.", result.Value.AgentSnapshot.SystemPrompt);
        Assert.Equal([result.Value.Id], fixture.Queue.Enqueued);

        var messages = await fixture.Runs.GetMessagesAsync(result.Value.Id, TestContext.Current.CancellationToken);

        Assert.Equal([MessageRole.System, MessageRole.User], messages.Value!.Select(m => m.Role));
        Assert.Equal([0, 1], messages.Value!.Select(m => m.Sequence));
    }

    [Fact]
    public async Task GetAsync_WhenMissing_ReturnsNotFound()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("run_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenRunMissing_ReturnsNotFound()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);

        var result = await fixture.Runs.GetMessagesAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("run_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ListAsync_WhenFiltered_RespectsAgentAndStatus()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var first = await fixture.Agents.CreateAsync(Definition("Alpha"), TestContext.Current.CancellationToken);
        var second = await fixture.Agents.CreateAsync(Definition("Bravo"), TestContext.Current.CancellationToken);

        var kept = await fixture.Runs.CreateAsync(first.Value!.Id, "Eins.", TestContext.Current.CancellationToken);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var cancelled = await fixture.Runs.CreateAsync(first.Value.Id, "Zwei.", TestContext.Current.CancellationToken);
        await fixture.Runs.CreateAsync(second.Value!.Id, "Drei.", TestContext.Current.CancellationToken);
        await fixture.Runs.CancelAsync(cancelled.Value!.Id, cancelled.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        var byAgent = await fixture.Runs.ListAsync(first.Value.Id, null, PageRequest.From(0, 10), TestContext.Current.CancellationToken);
        var byStatus = await fixture.Runs.ListAsync(null, RunStatus.Pending, PageRequest.From(0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(2, byAgent.Total);
        Assert.Equal(2, byStatus.Total);
        Assert.DoesNotContain(cancelled.Value.Id, byStatus.Items.Select(r => r.Id));
        Assert.Contains(kept.Value!.Id, byAgent.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task CancelAsync_WhenPending_CancelsRun()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);
        var cancelledAt = fixture.Clock.Advance(TimeSpan.FromSeconds(30));

        var result = await fixture.Runs.CancelAsync(run.Value!.Id, run.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(RunStatus.Cancelled, result.Value!.Status);
        Assert.Equal(cancelledAt, result.Value.CompletedAt);
    }

    [Fact]
    public async Task CancelAsync_WhenTokenStale_ReturnsConflict()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);

        var result = await fixture.Runs.CancelAsync(run.Value!.Id, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("concurrency_conflict", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyCancelled_ReturnsInvalidTransition()
    {
        using var database = new AgentsDatabase();
        using var fixture = NewFixture(database);
        var agent = await fixture.Agents.CreateAsync(Definition(), TestContext.Current.CancellationToken);
        var run = await fixture.Runs.CreateAsync(agent.Value!.Id, "Los.", TestContext.Current.CancellationToken);
        var cancelled = await fixture.Runs.CancelAsync(run.Value!.Id, run.Value.ConcurrencyToken, TestContext.Current.CancellationToken);

        var again = await fixture.Runs.CancelAsync(
            run.Value.Id,
            cancelled.Value!.ConcurrencyToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("run_invalid_transition", again.Error!.Value.Code);
    }
}
