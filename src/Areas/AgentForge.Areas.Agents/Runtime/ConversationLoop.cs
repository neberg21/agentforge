using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Tools;
using AgentForge.Areas.Agents.Runtime.Workspace;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime;

public sealed class ConversationLoop
{
    private readonly AgentsDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IToolRegistry _tools;
    private readonly IConversationEventBus _events;
    private readonly IClock _clock;
    private readonly AgentsOptions _options;

    public ConversationLoop(
        AgentsDbContext db,
        ILlmClient llm,
        IToolRegistry tools,
        IConversationEventBus events,
        IClock clock,
        IOptions<AgentsOptions> options)
    {
        _db = db;
        _llm = llm;
        _tools = tools;
        _events = events;
        _clock = clock;
        _options = options.Value;
    }

    public async Task ExecuteReplyAsync(
        Guid conversationId,
        Guid agentId,
        Guid streamId,
        CancellationToken ct)
    {
        var conversation = await LoadConversationAsync(conversationId, ct);
        if (conversation is null || conversation.IsArchived)
        {
            return;
        }

        var agent = await _db.Agents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == agentId, ct);
        if (agent is null || agent.IsArchived)
        {
            Publish(conversationId, RunEventType.Error, "{\"message\":\"agent unavailable\"}");
            return;
        }

        var allowedTools = _options.Workspace.Enabled
            ? (IReadOnlyList<string>)["read_file"]
            : Array.Empty<string>();

        Publish(conversationId, RunEventType.Status, "{\"status\":\"Running\"}");

        var turns = 0;
        try
        {
            while (turns < agent.MaxTurns)
            {
                ct.ThrowIfCancellationRequested();

                conversation = await LoadConversationAsync(conversationId, ct);
                if (conversation is null)
                {
                    return;
                }

                var history = await LoadHistoryAsync(conversationId, ct);
                var messages = BuildMessages(agent, history);
                var request = new LlmCompletionRequest(
                    agent.Model,
                    agent.Temperature,
                    agent.MaxOutputTokens,
                    messages,
                    allowedTools);
                var completion = await _llm.CompleteAsync(request, ct);

                var toolCallsJson = completion.ToolCalls.Count == 0
                    ? null
                    : SerializeToolCalls(completion.ToolCalls);

                var message = conversation.AppendMessage(
                    MessageRole.Assistant,
                    completion.Content,
                    _clock.UtcNow,
                    agent.Id,
                    agent.Name,
                    mentionsJson: null,
                    toolCallsJson,
                    toolCallId: null);
                _db.ConversationMessages.Add(message);
                await _db.SaveChangesAsync(ct);
                Publish(conversationId, RunEventType.Message, $"{{\"role\":\"Assistant\",\"streamId\":\"{streamId}\"}}");

                turns++;

                if (completion.ToolCalls.Count == 0)
                {
                    return;
                }

                foreach (var call in completion.ToolCalls)
                {
                    if (call.Name is not "read_file")
                    {
                        var denied = """{"ok":false,"error":"tool_not_allowed_in_conversation"}""";
                        var deniedMessage = conversation.AppendMessage(
                            MessageRole.Tool,
                            denied,
                            _clock.UtcNow,
                            agent.Id,
                            agent.Name,
                            null,
                            null,
                            call.Id);
                        _db.ConversationMessages.Add(deniedMessage);
                        await _db.SaveChangesAsync(ct);
                        Publish(conversationId, RunEventType.Message, $"{{\"role\":\"Tool\",\"toolCallId\":\"{call.Id}\"}}");
                        continue;
                    }

                    var toolResult = await _tools.ExecuteOrErrorAsync(call.Name, call.ArgumentsJson, ct);
                    var toolMessage = conversation.AppendMessage(
                        MessageRole.Tool,
                        toolResult,
                        _clock.UtcNow,
                        agent.Id,
                        agent.Name,
                        null,
                        null,
                        call.Id);
                    _db.ConversationMessages.Add(toolMessage);
                    await _db.SaveChangesAsync(ct);
                    Publish(conversationId, RunEventType.Message, $"{{\"role\":\"Tool\",\"toolCallId\":\"{call.Id}\"}}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Publish(conversationId, RunEventType.Error, $"{{\"message\":\"{Escape(ex.Message)}\"}}");
        }
    }

    private async Task<Conversation?> LoadConversationAsync(Guid conversationId, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        return await _db.Conversations
            .IgnoreQueryFilters()
            .Include(conversation => conversation.Participants)
            .Include(conversation => conversation.Messages)
            .FirstOrDefaultAsync(candidate => candidate.Id == conversationId, ct);
    }

    private async Task<IReadOnlyList<ConversationMessage>> LoadHistoryAsync(
        Guid conversationId,
        CancellationToken ct)
    {
        return await _db.ConversationMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.Sequence)
            .ToListAsync(ct);
    }

    private static List<LlmMessage> BuildMessages(Agent agent, IReadOnlyList<ConversationMessage> history)
    {
        var messages = new List<LlmMessage>
        {
            new("system", agent.SystemPrompt, null, null)
        };

        foreach (var message in history)
        {
            var role = message.Role switch
            {
                MessageRole.User => "user",
                MessageRole.Assistant => "assistant",
                MessageRole.Tool => "tool",
                MessageRole.System => "system",
                _ => "user"
            };
            var llmMessage = new LlmMessage(role, message.Content, message.ToolCallsJson, message.ToolCallId);
            messages.Add(llmMessage);
        }

        return messages;
    }

    private void Publish(Guid conversationId, RunEventType type, string payload)
    {
        var ev = new ConversationEvent(conversationId, type, payload);
        _events.Publish(ev);
    }

    private static string SerializeToolCalls(IReadOnlyList<LlmToolCall> toolCalls)
    {
        var shaped = toolCalls.Select(call => new
        {
            id = call.Id,
            type = "function",
            function = new { name = call.Name, arguments = call.ArgumentsJson }
        });

        return JsonSerializer.Serialize(shaped);
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
