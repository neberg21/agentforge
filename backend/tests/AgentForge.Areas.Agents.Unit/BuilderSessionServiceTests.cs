using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
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
        AgentService Agents,
        ConversationService Conversations) NewServices(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var queue = new RecordingReplyQueue();
        var events = new InProcessConversationEventBus();
        var llm = new ScriptedLlmClient(
            [new LlmCompletionResult("ok", [], new LlmUsage(1, 1))]);
        var conversations = new ConversationService(
            context, database.CurrentUser, clock, queue, new ChannelConversationTitleQueue(), events, llm);
        var nameSource = new BogusGermanFirstNameSource();
        var suggestions = new AgentSuggestionService(agents, nameSource);
        var builder = new BuilderSessionService(agents, conversations, suggestions);
        return (context, builder, agents, conversations);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderMissing_CreatesBuilderAndConversation()
    {
        using var database = new AgentsDatabase();
        var (context, builder, _, _) = NewServices(database, TestClock.AtEpoch());
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
        var (context, builder, _, _) = NewServices(database, TestClock.AtEpoch());
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
        var (context, builder, agents, _) = NewServices(database, TestClock.AtEpoch());
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

    [Fact]
    public async Task StartAsync_WhenCalled_SeedsSystemMessageWithSuggestedName()
    {
        using var database = new AgentsDatabase();
        var (context, builder, _, conversations) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var messages = await conversations.GetMessagesAsync(
            result.Value!.ConversationId,
            TestContext.Current.CancellationToken);
        Assert.True(messages.IsSuccess);
        var system = Assert.Single(messages.Value!);
        Assert.Equal(MessageRole.System, system.Role);
        Assert.StartsWith("Suggested agent name for this session:", system.Content);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderExistsWithOldPrompt_UpdatesSystemPrompt()
    {
        using var database = new AgentsDatabase();
        var (context, builder, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;

        var stale = new AgentDefinition(
            AgentBuilderDefaults.Name,
            "old",
            "OLD PROMPT THAT ASKS FOR A NAME",
            AgentBuilderDefaults.Model,
            Agent.DefaultTemperature,
            Agent.DefaultMaxOutputTokens,
            Agent.DefaultMaxTurns,
            []);
        var created = await agents.CreateAsync(stale, TestContext.Current.CancellationToken);

        var result = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var reloaded = await agents.GetAsync(created.Value!.Id, TestContext.Current.CancellationToken);
        Assert.Equal(AgentBuilderDefaults.SystemPrompt, reloaded.Value!.SystemPrompt);
        Assert.DoesNotContain("Cover essentials first: name", reloaded.Value.SystemPrompt);
    }
}
