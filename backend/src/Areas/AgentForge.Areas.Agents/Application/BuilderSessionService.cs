using AgentForge.Areas.Agents.Domain;
using AgentForge.Core;

namespace AgentForge.Areas.Agents.Application;

public sealed record BuilderSession(Guid ConversationId, Guid BuilderAgentId);

public sealed class BuilderSessionService
{
    private readonly AgentService _agents;
    private readonly ConversationService _conversations;
    private readonly AgentSuggestionService _suggestions;

    public BuilderSessionService(
        AgentService agents,
        ConversationService conversations,
        AgentSuggestionService suggestions)
    {
        _agents = agents;
        _conversations = conversations;
        _suggestions = suggestions;
    }

    public async Task<Result<BuilderSession>> StartAsync(CancellationToken ct)
    {
        var existing = await _agents.FindActiveByNameAsync(AgentBuilderDefaults.Name, ct);
        Agent builder;
        if (existing is null)
        {
            var created = await _agents.CreateAsync(AgentBuilderDefaults.Definition, ct);
            if (!created.IsSuccess)
            {
                return created.Error!.Value;
            }

            builder = created.Value!;
        }
        else
        {
            var updated = await _agents.UpdateAsync(
                existing.Id,
                AgentBuilderDefaults.Definition,
                existing.ConcurrencyToken,
                ct);
            if (!updated.IsSuccess)
            {
                return updated.Error!.Value;
            }

            builder = updated.Value!;
        }

        var suggestedName = await _suggestions.SuggestNameAsync(ct);
        var systemMessage = AgentBuilderDefaults.FormatSuggestedNameMessage(suggestedName);

        var participantIds = new[] { builder.Id };
        var conversation = await _conversations.CreateAsync(
            AgentBuilderDefaults.ConversationTitle,
            participantIds,
            systemMessage,
            ct);
        if (!conversation.IsSuccess)
        {
            return conversation.Error!.Value;
        }

        var session = new BuilderSession(conversation.Value!.Id, builder.Id);
        return session;
    }
}
