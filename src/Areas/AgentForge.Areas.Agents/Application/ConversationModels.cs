using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Application;

public sealed record ConversationParticipantInfo(Guid AgentId, string Name);

public sealed record ConversationListItem(
    Conversation Conversation,
    IReadOnlyList<ConversationParticipantInfo> Participants,
    string? LastMessageExcerpt,
    DateTimeOffset? LastMessageAt);

public sealed record ConversationDetail(
    Conversation Conversation,
    IReadOnlyList<ConversationParticipantInfo> Participants);
