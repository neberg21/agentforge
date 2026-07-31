using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Tools;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationLoopTests
{
    private static AgentDefinition Definition() =>
        new("leo", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 5, ["read_file", "write_file"]);

    [Fact]
    public async Task ExecuteReplyAsync_WhenAssistantReplies_AppendsMessageWithSender()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), clock.UtcNow);
        var conversation = Conversation.Create(
            database.CurrentUser.OwnerId,
            "chat",
            TitleMode.Locked,
            [agent.Id],
            clock.UtcNow);
        conversation.AppendMessage(
            MessageRole.User,
            "Hallo",
            clock.UtcNow,
            null,
            null,
            $"[\"{agent.Id}\"]",
            null,
            null);

        await using (var seed = database.NewContext())
        {
            seed.Agents.Add(agent);
            seed.Conversations.Add(conversation);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var completion = new LlmCompletionResult("Hi!", [], new LlmUsage(1, 1));
        var llm = new ScriptedLlmClient([completion]);
        var tools = new ToolRegistry();
        var events = new InProcessConversationEventBus();
        var options = Options.Create(new AgentsOptions());
        await using var context = database.NewContext();
        var loop = new ConversationLoop(context, llm, tools, events, clock, options);

        await loop.ExecuteReplyAsync(
            conversation.Id,
            agent.Id,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var messages = await verify.ConversationMessages
            .OrderBy(message => message.Sequence)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, messages.Count);
        Assert.Equal(MessageRole.Assistant, messages[1].Role);
        Assert.Equal("Hi!", messages[1].Content);
        Assert.Equal(agent.Id, messages[1].SenderAgentId);
        Assert.Equal("leo", messages[1].SenderName);
    }

    [Fact]
    public async Task ExecuteReplyAsync_WhenReadFileTool_UsesConversationReadContext()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), clock.UtcNow);
        var conversation = Conversation.Create(
            database.CurrentUser.OwnerId,
            "chat",
            TitleMode.Locked,
            [agent.Id],
            clock.UtcNow);
        conversation.AppendMessage(
            MessageRole.User,
            "lies note.txt",
            clock.UtcNow,
            null,
            null,
            null,
            null,
            null);

        await using (var seed = database.NewContext())
        {
            seed.Agents.Add(agent);
            seed.Conversations.Add(conversation);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var root = Path.Combine(Path.GetTempPath(), "conv-read-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "note.txt"),
                "hello-file",
                TestContext.Current.CancellationToken);

            ConversationReadContext.Current = new ConversationReadContext(root);
            try
            {
                var toolCall = new LlmToolCall("call-1", "read_file", """{"path":"note.txt"}""");
                var withTool = new LlmCompletionResult(null, [toolCall], new LlmUsage(1, 1));
                var final = new LlmCompletionResult("done", [], new LlmUsage(1, 1));
                var llm = new ScriptedLlmClient([withTool, final]);
                var tools = new ToolRegistry();
                tools.Register(new ReadFileTool());
                var events = new InProcessConversationEventBus();
                var agentsOptions = new AgentsOptions();
                agentsOptions.Workspace.Enabled = true;
                var options = Options.Create(agentsOptions);
                await using var context = database.NewContext();
                var loop = new ConversationLoop(context, llm, tools, events, clock, options);

                await loop.ExecuteReplyAsync(
                    conversation.Id,
                    agent.Id,
                    Guid.CreateVersion7(),
                    TestContext.Current.CancellationToken);

                await using var verify = database.NewContext();
                var toolMessage = await verify.ConversationMessages
                    .SingleAsync(
                        message => message.Role == MessageRole.Tool,
                        TestContext.Current.CancellationToken);
                Assert.Contains("hello-file", toolMessage.Content, StringComparison.Ordinal);
            }
            finally
            {
                ConversationReadContext.Current = null;
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteReplyAsync_WhenWriteFileRequested_DeniesTool()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), clock.UtcNow);
        var conversation = Conversation.Create(
            database.CurrentUser.OwnerId,
            "chat",
            TitleMode.Locked,
            [agent.Id],
            clock.UtcNow);
        conversation.AppendMessage(
            MessageRole.User,
            "schreib",
            clock.UtcNow,
            null,
            null,
            null,
            null,
            null);

        await using (var seed = database.NewContext())
        {
            seed.Agents.Add(agent);
            seed.Conversations.Add(conversation);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var toolCall = new LlmToolCall("call-1", "write_file", """{"path":"x","content":"y"}""");
        var withTool = new LlmCompletionResult(null, [toolCall], new LlmUsage(1, 1));
        var final = new LlmCompletionResult("ok", [], new LlmUsage(1, 1));
        var llm = new ScriptedLlmClient([withTool, final]);
        var tools = new ToolRegistry();
        tools.Register(new WriteFileTool());
        var events = new InProcessConversationEventBus();
        var agentsOptions = new AgentsOptions();
        agentsOptions.Workspace.Enabled = true;
        var options = Options.Create(agentsOptions);
        await using var context = database.NewContext();
        var loop = new ConversationLoop(context, llm, tools, events, clock, options);

        await loop.ExecuteReplyAsync(
            conversation.Id,
            agent.Id,
            Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var toolMessage = await verify.ConversationMessages
            .SingleAsync(
                message => message.Role == MessageRole.Tool,
                TestContext.Current.CancellationToken);
        Assert.Contains("tool_not_allowed_in_conversation", toolMessage.Content, StringComparison.Ordinal);
    }
}
