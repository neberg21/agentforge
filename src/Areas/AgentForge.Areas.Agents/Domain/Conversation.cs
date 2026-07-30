namespace AgentForge.Areas.Agents.Domain;

public sealed class Conversation
{
    private readonly List<ConversationParticipant> _participants = [];
    private readonly List<ConversationMessage> _messages = [];

    private Conversation()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<ConversationParticipant> Participants => _participants;

    public IReadOnlyList<ConversationMessage> Messages => _messages;

    public static Conversation Create(
        string ownerId,
        string title,
        IReadOnlyList<Guid> participantAgentIds,
        DateTimeOffset now)
    {
        if (participantAgentIds.Count == 0)
        {
            throw new ArgumentException("A conversation needs at least one participant.", nameof(participantAgentIds));
        }

        var conversation = new Conversation
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now,
            ConcurrencyToken = Guid.CreateVersion7()
        };

        foreach (var agentId in participantAgentIds.Distinct())
        {
            var participant = ConversationParticipant.Create(conversation.Id, agentId);
            conversation._participants.Add(participant);
        }

        return conversation;
    }

    public void Archive(DateTimeOffset now)
    {
        ArchivedAt = now;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Update(
        string title,
        IReadOnlyList<Guid> participantAgentIds,
        Guid concurrencyToken,
        DateTimeOffset now)
    {
        if (concurrencyToken != ConcurrencyToken)
        {
            throw new InvalidOperationException("Concurrency token mismatch.");
        }

        if (participantAgentIds.Count == 0)
        {
            throw new ArgumentException("A conversation needs at least one participant.", nameof(participantAgentIds));
        }

        Title = title;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();

        _participants.Clear();
        foreach (var agentId in participantAgentIds.Distinct())
        {
            var participant = ConversationParticipant.Create(Id, agentId);
            _participants.Add(participant);
        }
    }

    public ConversationMessage AppendMessage(
        MessageRole role,
        string? content,
        DateTimeOffset now,
        Guid? senderAgentId,
        string? senderName,
        string? mentionsJson,
        string? toolCallsJson,
        string? toolCallId)
    {
        var message = ConversationMessage.Create(
            this,
            _messages.Count,
            role,
            content,
            now,
            senderAgentId,
            senderName,
            mentionsJson,
            toolCallsJson,
            toolCallId);
        _messages.Add(message);
        UpdatedAt = now;
        return message;
    }
}
