using AgentForge.Areas.Agents.Domain;
using AgentForge.Core;

namespace AgentForge.Areas.Agents.Application;

public sealed record BuilderSession(Guid ConversationId, Guid BuilderAgentId);

public sealed class BuilderSessionService
{
    private readonly AgentService _agents;
    private readonly ConversationService _conversations;

    public BuilderSessionService(AgentService agents, ConversationService conversations)
    {
        _agents = agents;
        _conversations = conversations;
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
            builder = existing;
        }

        var participantIds = new[] { builder.Id };
        var conversation = await _conversations.CreateAsync(
            AgentBuilderDefaults.ConversationTitle,
            participantIds,
            ct);
        if (!conversation.IsSuccess)
        {
            return conversation.Error!.Value;
        }

        var session = new BuilderSession(conversation.Value!.Id, builder.Id);
        return session;
    }
}
