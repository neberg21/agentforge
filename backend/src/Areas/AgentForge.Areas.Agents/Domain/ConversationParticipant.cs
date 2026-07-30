namespace AgentForge.Areas.Agents.Domain;

public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    public Guid ConversationId { get; private set; }

    public Guid AgentId { get; private set; }

    internal static ConversationParticipant Create(Guid conversationId, Guid agentId) =>
        new()
        {
            ConversationId = conversationId,
            AgentId = agentId
        };
}
