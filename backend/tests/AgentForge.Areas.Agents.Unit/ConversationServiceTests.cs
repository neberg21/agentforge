using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationServiceTests
{
    private static AgentDefinition Definition(string name) =>
        new(name, null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    private sealed class RecordingReplyQueue : IConversationReplyQueue
    {
        public List<ConversationReplyJob> Jobs { get; } = [];

        public void Enqueue(ConversationReplyJob job) => Jobs.Add(job);

        public async IAsyncEnumerable<ConversationReplyJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private static (
        AgentsDbContext Context,
        ConversationService Conversations,
        AgentService Agents,
        RecordingReplyQueue Queue) NewServices(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var queue = new RecordingReplyQueue();
        var events = new InProcessConversationEventBus();
        var llm = new ScriptedLlmClient([new LlmCompletionResult("Ship the feature.", [], new LlmUsage(1, 1))]);
        var conversations = new ConversationService(
            context,
            database.CurrentUser,
            clock,
            queue,
            events,
            llm);
        return (context, conversations, agents, queue);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentsExist_CreatesWithDefaultTitleFromNames()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var (context, conversations, agents, _) = NewServices(database, clock);
        await using var _ = context;
        var leo = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var max = await agents.CreateAsync(Definition("max"), TestContext.Current.CancellationToken);
        var ids = new[] { leo.Value!.Id, max.Value!.Id };

        var result = await conversations.CreateAsync(null, ids, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("leo, max", result.Value!.Title);
        Assert.Equal(2, result.Value.Participants.Count);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentArchived_ReturnsAgentArchived()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var created = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        await agents.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);
        var ids = new[] { created.Value.Id };

        var result = await conversations.CreateAsync("x", ids, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("agent_archived", result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateAsync_WhenAgentMissing_ReturnsNotFound()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, _, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var ids = new[] { Guid.CreateVersion7() };

        var result = await conversations.CreateAsync("x", ids, TestContext.Current.CancellationToken);

        Assert.Equal("agent_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task ListAsync_WhenArchived_ExcludesThem()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var keep = await conversations.CreateAsync("keep", ids, TestContext.Current.CancellationToken);
        var drop = await conversations.CreateAsync("drop", ids, TestContext.Current.CancellationToken);
        await conversations.ArchiveAsync(drop.Value!.Id, TestContext.Current.CancellationToken);

        var page = await conversations.ListAsync(PageRequest.From(0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(1, page.Total);
        Assert.Equal(keep.Value!.Id, page.Items[0].Conversation.Id);
        Assert.Equal("leo", page.Items[0].Participants[0].Name);
    }

    [Fact]
    public async Task ArchiveAsync_WhenAlreadyArchived_IsIdempotent()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var created = await conversations.CreateAsync("c", ids, TestContext.Current.CancellationToken);
        await conversations.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var again = await conversations.ArchiveAsync(created.Value.Id, TestContext.Current.CancellationToken);

        Assert.True(again.IsSuccess);
        Assert.True(again.Value!.IsArchived);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenMissing_ReturnsNotFound()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, _, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await conversations.GetMessagesAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        Assert.Equal("conversation_not_found", result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateAsync_WhenTokenStale_ReturnsConcurrencyConflict()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var created = await conversations.CreateAsync("c", ids, TestContext.Current.CancellationToken);

        var result = await conversations.UpdateAsync(
            created.Value!.Id,
            "neu",
            ids,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        Assert.Equal("concurrency_conflict", result.Error!.Value.Code);
    }

    [Fact]
    public async Task PostMessageAsync_WhenNoMentions_StoresNoteWithoutEnqueue()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, queue) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var created = await conversations.CreateAsync("c", ids, TestContext.Current.CancellationToken);

        var result = await conversations.PostMessageAsync(
            created.Value!.Id,
            "note only",
            [],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(queue.Jobs);
        var messages = await conversations.GetMessagesAsync(created.Value.Id, TestContext.Current.CancellationToken);
        Assert.Single(messages.Value!);
    }

    [Fact]
    public async Task PostMessageAsync_WhenMentioned_EnqueuesReplyJob()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, queue) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var created = await conversations.CreateAsync("c", ids, TestContext.Current.CancellationToken);
        var mentions = new[] { agent.Value.Id };

        var result = await conversations.PostMessageAsync(
            created.Value!.Id,
            "hi @leo",
            mentions,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(queue.Jobs);
        Assert.Equal(agent.Value.Id, queue.Jobs[0].AgentIds[0]);
    }

    [Fact]
    public async Task DraftRunAsync_WhenCalled_ReturnsObjectiveAndAgent()
    {
        using var database = new AgentsDatabase();
        var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var ids = new[] { agent.Value!.Id };
        var created = await conversations.CreateAsync("c", ids, TestContext.Current.CancellationToken);
        await conversations.PostMessageAsync(
            created.Value!.Id,
            "we should build X",
            [],
            TestContext.Current.CancellationToken);

        var result = await conversations.DraftRunAsync(
            created.Value.Id,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(agent.Value.Id, result.Value!.AgentId);
        Assert.Equal("Ship the feature.", result.Value.Objective);
    }
}
