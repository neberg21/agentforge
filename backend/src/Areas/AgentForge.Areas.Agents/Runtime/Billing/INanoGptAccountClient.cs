namespace AgentForge.Areas.Agents.Runtime.Billing;

public interface INanoGptAccountClient
{
    Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct);

    Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct);

    Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct);

    Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct);

    Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct);
}
