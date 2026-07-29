namespace AgentForge.Areas.Agents.Domain;

public sealed class RunMessage
{
    private RunMessage()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid RunId { get; private set; }

    public int Sequence { get; private set; }

    public MessageRole Role { get; private set; }

    public string? Content { get; private set; }

    public string? ToolCallsJson { get; private set; }

    public string? ToolCallId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static RunMessage Create(
        Run run,
        int sequence,
        MessageRole role,
        string? content,
        DateTimeOffset now,
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

        return new RunMessage
        {
            Id = Guid.CreateVersion7(),
            OwnerId = run.OwnerId,
            RunId = run.Id,
            Sequence = sequence,
            Role = role,
            Content = content,
            ToolCallsJson = toolCallsJson,
            ToolCallId = toolCallId,
            CreatedAt = now
        };
    }
}
