namespace AgentForge.Areas.Agents.Domain;

public sealed class Conversation
{
    public const string DefaultAutoTitle = "New conversation";

    private readonly List<ConversationParticipant> _participants = [];
    private readonly List<ConversationMessage> _messages = [];

    private Conversation()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public TitleMode TitleMode { get; private set; }

    public int CompletedTurnCount { get; private set; }

    public int? TitleGeneratedAtTurn { get; private set; }

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
        TitleMode titleMode,
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
            TitleMode = titleMode,
            CompletedTurnCount = 0,
            TitleGeneratedAtTurn = null,
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

        if (TitleMode == TitleMode.Auto && !string.Equals(Title, title, StringComparison.Ordinal))
        {
            TitleMode = TitleMode.Paused;
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

    public bool ShouldSuggestTitle()
    {
        if (TitleMode != TitleMode.Auto)
        {
            return false;
        }

        if (TitleGeneratedAtTurn is null)
        {
            return CompletedTurnCount >= 1;
        }

        return CompletedTurnCount - TitleGeneratedAtTurn.Value >= 3;
    }

    public void RecordCompletedTurn(DateTimeOffset now)
    {
        CompletedTurnCount++;
        UpdatedAt = now;
    }

    public bool ApplySuggestedTitle(string title, DateTimeOffset now)
    {
        if (TitleMode != TitleMode.Auto)
        {
            return false;
        }

        Title = title;
        TitleGeneratedAtTurn = CompletedTurnCount;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
        return true;
    }

    public void SetTitle(string title, Guid concurrencyToken, DateTimeOffset now)
    {
        EnsureConcurrency(concurrencyToken);

        if (TitleMode == TitleMode.Auto)
        {
            TitleMode = TitleMode.Paused;
        }

        Title = title;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void LockTitle(Guid concurrencyToken, DateTimeOffset now)
    {
        EnsureConcurrency(concurrencyToken);
        TitleMode = TitleMode.Locked;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void ResumeAutoTitle(Guid concurrencyToken, DateTimeOffset now)
    {
        EnsureConcurrency(concurrencyToken);
        TitleMode = TitleMode.Auto;
        UpdatedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
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

    private void EnsureConcurrency(Guid concurrencyToken)
    {
        if (concurrencyToken != ConcurrencyToken)
        {
            throw new InvalidOperationException("Concurrency token mismatch.");
        }
    }
}
