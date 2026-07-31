using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationTitleServiceTests
{
    private static AgentDefinition Definition(string name) =>
        new(name, null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    private sealed class RecordingEventBus : IConversationEventBus
    {
        public List<ConversationEvent> Published { get; } = [];

        public void Publish(ConversationEvent ev) => Published.Add(ev);

        public async IAsyncEnumerable<ConversationEvent> Subscribe(
            Guid conversationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    [Fact]
    public async Task SuggestAndApplyAsync_WhenAuto_UpdatesTitleAndPublishesEvent()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        await using var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var agent = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
        var agentIds = new[] { agent.Value!.Id };
        var conversation = Conversation.Create(
            database.CurrentUser.OwnerId,
            Conversation.DefaultAutoTitle,
            TitleMode.Auto,
            agentIds,
            clock.UtcNow);
        conversation.AppendMessage(
            MessageRole.User,
            "Fix the login timeout",
            clock.UtcNow,
            null,
            null,
            $"[\"{agent.Value.Id}\"]",
            null,
            null);
        conversation.AppendMessage(
            MessageRole.Assistant,
            "I will look into the auth timeout.",
            clock.UtcNow,
            agent.Value.Id,
            "leo",
            null,
            null,
            null);
        conversation.RecordCompletedTurn(clock.UtcNow);
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var events = new RecordingEventBus();
        var llm = new ScriptedLlmClient(
            [new LlmCompletionResult("Auth timeout", [], new LlmUsage(1, 1))]);
        var options = Options.Create(new AgentsOptions
        {
            Llm = new AgentsLlmOptions
            {
                BaseUrl = "http://localhost",
                TitleModel = "gpt-4.1-nano"
            },
            Pricing = new AgentsPricingOptions()
        });
        var service = new ConversationTitleService(
            context,
            llm,
            options,
            events,
            clock,
            NullLogger<ConversationTitleService>.Instance);

        await service.SuggestAndApplyAsync(conversation.Id, TestContext.Current.CancellationToken);

        await context.Entry(conversation).ReloadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Auth timeout", conversation.Title);
        Assert.Equal(1, conversation.TitleGeneratedAtTurn);
        Assert.Single(events.Published);
        Assert.Equal(RunEventType.Title, events.Published[0].Type);
    }
}
