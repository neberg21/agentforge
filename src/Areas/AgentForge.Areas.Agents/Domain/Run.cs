namespace AgentForge.Areas.Agents.Domain;

public sealed class Run
{
    private readonly List<RunMessage> _messages = [];

    private Run()
    {
    }

    public Guid Id { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public Guid AgentId { get; private set; }

    public AgentSnapshot AgentSnapshot { get; private set; } = null!;

    public string Objective { get; private set; } = string.Empty;

    public RunStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? Error { get; private set; }

    public int? PromptTokens { get; private set; }

    public int? CompletionTokens { get; private set; }

    public decimal? CostEstimate { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public Guid? ConversationId { get; private set; }

    public IReadOnlyList<RunMessage> Messages => _messages;

    public static Run Create(Agent agent, string objective, DateTimeOffset now, Guid? conversationId = null)
    {
        var run = new Run
        {
            Id = Guid.CreateVersion7(),
            OwnerId = agent.OwnerId,
            AgentId = agent.Id,
            AgentSnapshot = agent.ToSnapshot(),
            Objective = objective,
            Status = RunStatus.Pending,
            CreatedAt = now,
            ConcurrencyToken = Guid.CreateVersion7(),
            ConversationId = conversationId
        };

        run.AppendMessage(MessageRole.System, run.AgentSnapshot.SystemPrompt, now);
        run.AppendMessage(MessageRole.User, objective, now);

        return run;
    }

    public RunMessage AppendMessage(
        MessageRole role,
        string? content,
        DateTimeOffset now,
        string? toolCallsJson = null,
        string? toolCallId = null)
    {
        var message = RunMessage.Create(this, _messages.Count, role, content, now, toolCallsJson, toolCallId);
        _messages.Add(message);
        return message;
    }

    public bool CanTransitionTo(RunStatus target) => RunTransitions.IsAllowed(Status, target);

    public void Cancel(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Cancelled))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Cancelled}.");
        }

        Status = RunStatus.Cancelled;
        CompletedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void MarkRunning(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Running))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Running}.");
        }

        Status = RunStatus.Running;
        StartedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Complete(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Completed))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Completed}.");
        }

        Status = RunStatus.Completed;
        CompletedAt = now;
        Error = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Fail(string error, DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Failed))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Failed}.");
        }

        Status = RunStatus.Failed;
        Error = error;
        CompletedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void ApplyUsage(int promptDelta, int completionDelta, decimal costEstimate)
    {
        PromptTokens = (PromptTokens ?? 0) + promptDelta;
        CompletionTokens = (CompletionTokens ?? 0) + completionDelta;
        CostEstimate = costEstimate;
    }
}
