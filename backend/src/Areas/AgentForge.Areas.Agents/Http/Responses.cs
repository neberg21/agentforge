using AgentForge.Areas.Agents.Application;
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
    Guid ConcurrencyToken,
    Guid? ConversationId)
{
    public static RunResponse From(Run run) =>
        new(run.Id, run.AgentId, AgentSnapshotResponse.From(run.AgentSnapshot), run.Objective,
            run.Status.ToString(), run.CreatedAt, run.StartedAt, run.CompletedAt, run.Error,
            run.PromptTokens, run.CompletionTokens, run.CostEstimate, run.ConcurrencyToken,
            run.ConversationId);
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

public sealed record ConversationParticipantResponse(Guid AgentId, string Name);

public sealed record ConversationResponse(
    Guid Id,
    string Title,
    string TitleMode,
    IReadOnlyList<ConversationParticipantResponse> Participants,
    string? LastMessageExcerpt,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyToken)
{
    private static string FormatTitleMode(TitleMode mode) =>
        mode.ToString().ToLowerInvariant();

    public static ConversationResponse From(ConversationListItem item) =>
        new(
            item.Conversation.Id,
            item.Conversation.Title,
            FormatTitleMode(item.Conversation.TitleMode),
            item.Participants.Select(participant =>
                new ConversationParticipantResponse(participant.AgentId, participant.Name)).ToArray(),
            item.LastMessageExcerpt,
            item.LastMessageAt,
            item.Conversation.CreatedAt,
            item.Conversation.UpdatedAt,
            item.Conversation.ArchivedAt,
            item.Conversation.ConcurrencyToken);

    public static ConversationResponse From(ConversationDetail detail) =>
        new(
            detail.Conversation.Id,
            detail.Conversation.Title,
            FormatTitleMode(detail.Conversation.TitleMode),
            detail.Participants.Select(participant =>
                new ConversationParticipantResponse(participant.AgentId, participant.Name)).ToArray(),
            null,
            null,
            detail.Conversation.CreatedAt,
            detail.Conversation.UpdatedAt,
            detail.Conversation.ArchivedAt,
            detail.Conversation.ConcurrencyToken);

    public static ConversationResponse From(Conversation conversation, IReadOnlyList<ConversationParticipantResponse> participants) =>
        new(
            conversation.Id,
            conversation.Title,
            FormatTitleMode(conversation.TitleMode),
            participants,
            null,
            null,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.ArchivedAt,
            conversation.ConcurrencyToken);
}

public sealed record ConversationMessageResponse(
    Guid Id,
    int Sequence,
    string Role,
    string? Content,
    string? ToolCallsJson,
    string? ToolCallId,
    Guid? SenderAgentId,
    string? SenderName,
    DateTimeOffset CreatedAt,
    Guid[]? Mentions)
{
    public static ConversationMessageResponse From(ConversationMessage message)
    {
        Guid[]? mentions = null;
        if (!string.IsNullOrWhiteSpace(message.MentionsJson))
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(message.MentionsJson);
            mentions = parsed is { Length: > 0 } ? parsed : null;
        }

        return new ConversationMessageResponse(
            message.Id,
            message.Sequence,
            message.Role.ToString(),
            message.Content,
            message.ToolCallsJson,
            message.ToolCallId,
            message.SenderAgentId,
            message.SenderName,
            message.CreatedAt,
            mentions);
    }
}

public sealed record PostMessageAcceptedResponse(Guid StreamId);

public sealed record DraftRunResponse(string Objective, Guid AgentId);

public sealed record BuilderSessionResponse(Guid ConversationId, Guid BuilderAgentId)
{
    public static BuilderSessionResponse From(BuilderSession session) =>
        new(session.ConversationId, session.BuilderAgentId);
}

public sealed record AgentSuggestionsResponse(string Name)
{
    public static AgentSuggestionsResponse From(string name) => new(name);
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take);
