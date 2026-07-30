using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Http;

public sealed record AgentResponse(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyToken)
{
    public static AgentResponse From(Agent agent) =>
        new(agent.Id, agent.Name, agent.Description, agent.SystemPrompt, agent.Model, agent.Temperature,
            agent.MaxOutputTokens, agent.MaxTurns, agent.AllowedTools, agent.CreatedAt, agent.UpdatedAt,
            agent.ArchivedAt, agent.ConcurrencyToken);
}

public sealed record AgentSnapshotResponse(
    string Name,
    string SystemPrompt,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    int MaxTurns,
    string[] AllowedTools)
{
    public static AgentSnapshotResponse From(AgentSnapshot snapshot) =>
        new(snapshot.Name, snapshot.SystemPrompt, snapshot.Model, snapshot.Temperature,
            snapshot.MaxOutputTokens, snapshot.MaxTurns, snapshot.AllowedTools);
}

public sealed record RunResponse(
    Guid Id,
    Guid AgentId,
    AgentSnapshotResponse AgentSnapshot,
    string Objective,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    int? PromptTokens,
    int? CompletionTokens,
    decimal? CostEstimate,
    Guid ConcurrencyToken)
{
    public static RunResponse From(Run run) =>
        new(run.Id, run.AgentId, AgentSnapshotResponse.From(run.AgentSnapshot), run.Objective,
            run.Status.ToString(), run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
            run.PromptTokens, run.CompletionTokens, run.CostEstimate, run.ConcurrencyToken);
}

public sealed record RunMessageResponse(
    Guid Id,
    int Sequence,
    string Role,
    string? Content,
    string? ToolCallsJson,
    string? ToolCallId,
    DateTimeOffset CreatedAt)
{
    public static RunMessageResponse From(RunMessage message) =>
        new(message.Id, message.Sequence, message.Role.ToString(), message.Content,
            message.ToolCallsJson, message.ToolCallId, message.CreatedAt);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
