using System.Collections.Concurrent;
using System.Net;

namespace AgentForge.Areas.Agents.Runtime.Billing;

public sealed class FakeNanoGptAccountClient : INanoGptAccountClient
{
    private readonly ConcurrentDictionary<string, NanoGptDeposit> _deposits = new(StringComparer.Ordinal);
    private int _txCounter;

    public Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct)
    {
        var balance = new NanoGptBalance(12.34m, 1.2m, "nano_fake");
        return Task.FromResult(balance);
    }

    public Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct)
    {
        var from = string.IsNullOrWhiteSpace(query.From) ? "2026-01-01" : query.From!;
        var to = string.IsNullOrWhiteSpace(query.To) ? "2026-01-31" : query.To!;
        var totals = new NanoGptUsageTotals(10, 1.5m, 0m, 1.5m, 1000, 200, 0, 1200);
        var usage = new NanoGptUsage(from, to, totals, null, null, null);
        return Task.FromResult(usage);
    }

    public Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct)
    {
        var limits = new NanoGptDepositLimits(0.00001m, 0.1m, 0.10m, 500m);
        return Task.FromResult(limits);
    }

    public Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct)
    {
        var next = Interlocked.Increment(ref _txCounter);
        var txId = $"fake-tx-{next}";
        var createdAt = new DateTimeOffset(2026, 1, 19, 12, 0, 0, TimeSpan.Zero);
        var expiresAt = createdAt.AddHours(1);
        var deposit = new NanoGptDeposit(
            txId,
            amount,
            "New",
            "lightning:fake",
            null,
            createdAt,
            expiresAt);
        _deposits[txId] = deposit;
        return Task.FromResult(deposit);
    }

    public Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct)
    {
        if (_deposits.TryGetValue(txId, out var deposit))
        {
            return Task.FromResult(deposit);
        }

        throw new NanoGptAccountException(HttpStatusCode.NotFound, "Deposit was not found.");
    }
}
