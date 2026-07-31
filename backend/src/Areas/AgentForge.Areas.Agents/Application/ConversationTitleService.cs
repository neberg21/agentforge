using System.Text;
using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Application;

public sealed class ConversationTitleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentsDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IOptions<AgentsOptions> _options;
    private readonly IConversationEventBus _events;
    private readonly IClock _clock;
    private readonly ILogger<ConversationTitleService> _logger;

    public ConversationTitleService(
        AgentsDbContext db,
        ILlmClient llm,
        IOptions<AgentsOptions> options,
        IConversationEventBus events,
        IClock clock,
        ILogger<ConversationTitleService> logger)
    {
        _db = db;
        _llm = llm;
        _options = options;
        _events = events;
        _clock = clock;
        _logger = logger;
    }

    public async Task SuggestAndApplyAsync(Guid conversationId, CancellationToken ct)
    {
        var conversation = await _db.Conversations
            .Include(candidate => candidate.Messages)
            .FirstOrDefaultAsync(candidate => candidate.Id == conversationId, ct);

        if (conversation is null || conversation.IsArchived || conversation.TitleMode != TitleMode.Auto)
        {
            return;
        }

        if (!conversation.ShouldSuggestTitle())
        {
            return;
        }

        string? rawTitle;
        try
        {
            var messages = BuildPromptMessages(conversation);
            var request = new LlmCompletionRequest(
                _options.Value.Llm.TitleModel,
                temperature: 0.3,
                maxOutputTokens: 32,
                messages,
                allowedToolNames: []);
            var completion = await _llm.CompleteAsync(request, ct);
            rawTitle = completion.Content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Title suggestion failed for conversation {ConversationId}.",
                conversationId);
            return;
        }

        var normalized = NormalizeTitle(rawTitle);
        if (normalized is null)
        {
            return;
        }

        await _db.Entry(conversation).ReloadAsync(ct);
        if (conversation.IsArchived || conversation.TitleMode != TitleMode.Auto)
        {
            return;
        }

        if (!conversation.ApplySuggestedTitle(normalized, _clock.UtcNow))
        {
            return;
        }

        await _db.SaveChangesAsync(ct);

        var payload = new
        {
            title = conversation.Title,
            titleMode = conversation.TitleMode.ToString().ToLowerInvariant(),
            concurrencyToken = conversation.ConcurrencyToken
        };
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var titleEvent = new ConversationEvent(conversation.Id, RunEventType.Title, json);
        _events.Publish(titleEvent);
    }

    private static IReadOnlyList<LlmMessage> BuildPromptMessages(Conversation conversation)
    {
        var recent = conversation.Messages
            .Where(message => message.Role != MessageRole.System)
            .OrderByDescending(message => message.Sequence)
            .Take(12)
            .OrderBy(message => message.Sequence)
            .ToList();

        var body = new StringBuilder();
        foreach (var message in recent)
        {
            var role = message.Role.ToString().ToLowerInvariant();
            var content = message.Content ?? string.Empty;
            body.Append(role);
            body.Append(": ");
            body.AppendLine(content);
        }

        var system = new LlmMessage(
            "system",
            "Reply with only a short conversation title. No quotes.",
            null,
            null);
        var user = new LlmMessage("user", body.ToString(), null, null);
        return [system, user];
    }

    private static string? NormalizeTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var title = raw.Trim();
        if (title.Length >= 2
            && ((title[0] == '"' && title[^1] == '"')
                || (title[0] == '\'' && title[^1] == '\'')))
        {
            title = title[1..^1].Trim();
        }

        if (title.Length == 0)
        {
            return null;
        }

        if (title.Length > 200)
        {
            title = title[..200].Trim();
        }

        return title.Length == 0 ? null : title;
    }
}
