using System.Net;

namespace AgentForge.Areas.Agents.Runtime.Billing;

public sealed record NanoGptBalance(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress);

public sealed record NanoGptUsageQuery(string? From, string? To, string? GroupBy);

public sealed record NanoGptUsageTotals(
    int Requests,
    decimal CostUsd,
    decimal RefundedUsd,
    decimal NetCostUsd,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens);

public sealed record NanoGptUsageBucket(
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

public sealed record NanoGptUsage(
    string From,
    string To,
    NanoGptUsageTotals Totals,
    IReadOnlyList<NanoGptUsageBucket>? ByDay,
    IReadOnlyList<NanoGptUsageBucket>? ByModel,
    IReadOnlyList<NanoGptUsageBucket>? ByDayModel);

public sealed record NanoGptDepositLimits(
    decimal Minimum,
    decimal Maximum,
    decimal? FiatEquivalentMinimum,
    decimal? FiatEquivalentMaximum);

public sealed record NanoGptDeposit(
    string TxId,
    decimal Amount,
    string Status,
    string? PaymentLink,
    string? Address,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed class NanoGptAccountException : Exception
{
    public NanoGptAccountException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
