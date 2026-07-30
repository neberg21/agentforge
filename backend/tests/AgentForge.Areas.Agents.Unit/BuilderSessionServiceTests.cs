using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class BuilderSessionServiceTests
{
    private sealed class RecordingReplyQueue : IConversationReplyQueue
    {
        public void Enqueue(ConversationReplyJob job)
        {
        }

        public async IAsyncEnumerable<ConversationReplyJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private static (
        AgentsDbContext Context,
        BuilderSessionService Builder,
        AgentService Agents) NewServices(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var queue = new RecordingReplyQueue();
        var events = new InProcessConversationEventBus();
        var llm = new ScriptedLlmClient(
            [new LlmCompletionResult("ok", [], new LlmUsage(1, 1))]);
        var conversations = new ConversationService(
            context, database.CurrentUser, clock, queue, events, llm);
        var builder = new BuilderSessionService(agents, conversations);
        return (context, builder, agents);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderMissing_CreatesBuilderAndConversation()
    {
        using var database = new AgentsDatabase();
        var (context, builder, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var agents = await context.Agents.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(agents, a => a.Name == AgentBuilderDefaults.Name && a.ArchivedAt == null);
        Assert.Equal(1, await context.Conversations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(result.Value!.BuilderAgentId, agents.Single(a => a.Name == AgentBuilderDefaults.Name).Id);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderExists_ReusesSameAgent()
    {
        using var database = new AgentsDatabase();
        var (context, builder, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var first = await builder.StartAsync(TestContext.Current.CancellationToken);
        var second = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.BuilderAgentId, second.Value!.BuilderAgentId);
        Assert.NotEqual(first.Value.ConversationId, second.Value.ConversationId);
        Assert.Equal(1, await context.Agents.CountAsync(
            a => a.Name == AgentBuilderDefaults.Name && a.ArchivedAt == null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_WhenBuilderArchived_RecreatesBuilder()
    {
        using var database = new AgentsDatabase();
        var (context, builder, agents) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var first = await builder.StartAsync(TestContext.Current.CancellationToken);
        await agents.ArchiveAsync(first.Value!.BuilderAgentId, TestContext.Current.CancellationToken);

        var second = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.BuilderAgentId, second.Value!.BuilderAgentId);
        Assert.Equal(2, await context.Agents.CountAsync(
            a => a.Name == AgentBuilderDefaults.Name,
            TestContext.Current.CancellationToken));
    }
}
