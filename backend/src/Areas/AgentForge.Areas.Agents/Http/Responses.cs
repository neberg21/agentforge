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
    IReadOnlyList<ConversationParticipantResponse> Participants,
    string? LastMessageExcerpt,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyToken)
{
    public static ConversationResponse From(ConversationListItem item) =>
        new(
            item.Conversation.Id,
            item.Conversation.Title,
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

public sealed record BillingBalanceResponse(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress,
    bool LowBalance,
    decimal LowBalanceThresholdUsd)
{
    public static BillingBalanceResponse From(BillingBalanceView view) =>
        new(view.UsdBalance, view.NanoBalance, view.NanoDepositAddress, view.LowBalance, view.LowBalanceThresholdUsd);
}

public sealed record BillingUsageTotalsResponse(
    int Requests,
    decimal CostUsd,
    decimal RefundedUsd,
    decimal NetCostUsd,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens);

public sealed record BillingUsageBucketResponse(
    string? Date,
    string? Model,
    int Requests,
    decimal CostUsd,
    decimal RefundedUsd,
    decimal NetCostUsd,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens);

public sealed record BillingUsageResponse(
    string From,
    string To,
    BillingUsageTotalsResponse Totals,
    IReadOnlyList<BillingUsageBucketResponse>? ByDay,
    IReadOnlyList<BillingUsageBucketResponse>? ByModel,
    IReadOnlyList<BillingUsageBucketResponse>? ByDayModel)
{
    public static BillingUsageResponse FromUsage(Runtime.Billing.NanoGptUsage usage) =>
        new(
            usage.From,
            usage.To,
            new BillingUsageTotalsResponse(
                usage.Totals.Requests,
                usage.Totals.CostUsd,
                usage.Totals.RefundedUsd,
                usage.Totals.NetCostUsd,
                usage.Totals.InputTokens,
                usage.Totals.OutputTokens,
                usage.Totals.ReasoningTokens,
                usage.Totals.TotalTokens),
            MapBuckets(usage.ByDay),
            MapBuckets(usage.ByModel),
            MapBuckets(usage.ByDayModel));

    private static IReadOnlyList<BillingUsageBucketResponse>? MapBuckets(
        IReadOnlyList<Runtime.Billing.NanoGptUsageBucket>? buckets) =>
        buckets is null
            ? null
            : buckets.Select(bucket => new BillingUsageBucketResponse(
                bucket.Date,
                bucket.Model,
                bucket.Requests,
                bucket.CostUsd,
                bucket.RefundedUsd,
                bucket.NetCostUsd,
                bucket.InputTokens,
                bucket.OutputTokens,
                bucket.ReasoningTokens,
                bucket.TotalTokens)).ToArray();
}

public sealed record BillingDepositLimitsResponse(
    decimal Minimum,
    decimal Maximum,
    decimal? FiatEquivalentMinimum,
    decimal? FiatEquivalentMaximum)
{
    public static BillingDepositLimitsResponse From(Runtime.Billing.NanoGptDepositLimits limits) =>
        new(limits.Minimum, limits.Maximum, limits.FiatEquivalentMinimum, limits.FiatEquivalentMaximum);
}

public sealed record BillingDepositResponse(
    string TxId,
    decimal Amount,
    string Status,
    string? PaymentLink,
    string? Address,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt)
{
    public static BillingDepositResponse From(Runtime.Billing.NanoGptDeposit deposit) =>
        new(
            deposit.TxId,
            deposit.Amount,
            deposit.Status,
            deposit.PaymentLink,
            deposit.Address,
            deposit.CreatedAt,
            deposit.ExpiresAt);
}
