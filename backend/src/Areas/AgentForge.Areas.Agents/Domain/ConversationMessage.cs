namespace AgentForge.Areas.Agents.Domain;

public sealed class ConversationMessage
{
    private ConversationMessage()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid ConversationId { get; private set; }

    public int Sequence { get; private set; }

    public MessageRole Role { get; private set; }

    public string? Content { get; private set; }

    public string? ToolCallsJson { get; private set; }

    public string? ToolCallId { get; private set; }

    public Guid? SenderAgentId { get; private set; }

    public string? SenderName { get; private set; }

    public string? MentionsJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static ConversationMessage Create(
        Conversation conversation,
        int sequence,
        MessageRole role,
        string? content,
        DateTimeOffset now,
        Guid? senderAgentId,
        string? senderName,
        string? mentionsJson,
        string? toolCallsJson,
        string? toolCallId)
    {
        if (role == MessageRole.Tool && string.IsNullOrEmpty(toolCallId))
        {
            throw new ArgumentException("Tool messages must carry a tool call id.", nameof(toolCallId));
        }

        if (role != MessageRole.Tool && toolCallId is not null)
        {
            throw new ArgumentException("Only tool messages may carry a tool call id.", nameof(toolCallId));
        }

        return new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            OwnerId = conversation.OwnerId,
            ConversationId = conversation.Id,
            Sequence = sequence,
            Role = role,
            Content = content,
            ToolCallsJson = toolCallsJson,
            ToolCallId = toolCallId,
            SenderAgentId = senderAgentId,
            SenderName = senderName,
            MentionsJson = mentionsJson,
            CreatedAt = now
        };
    }
}
