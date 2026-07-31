using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Billing;
using AgentForge.Core;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Application;

public sealed record BillingBalanceView(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress,
    bool LowBalance,
    decimal LowBalanceThresholdUsd);

public sealed class BillingService
{
    private readonly INanoGptAccountClient _account;
    private readonly AgentsOptions _options;

    public BillingService(INanoGptAccountClient account, IOptions<AgentsOptions> options)
    {
        _account = account;
        _options = options.Value;
    }

    public async Task<Result<BillingBalanceView>> GetBalanceAsync(CancellationToken ct)
    {
        try
        {
            var balance = await _account.GetBalanceAsync(ct);
            var threshold = _options.Billing.LowBalanceUsdThreshold;
            var low = BillingBalance.IsLow(balance.UsdBalance, threshold);
            var view = new BillingBalanceView(
                balance.UsdBalance,
                balance.NanoBalance,
                balance.NanoDepositAddress,
                low,
                threshold);
            return Result<BillingBalanceView>.Success(view);
        }
        catch (Exception ex) when (ex is NanoGptAccountException or HttpRequestException or OperationCanceledException)
        {
            return MapFailure<BillingBalanceView>(ex, ct);
        }
    }

    public async Task<Result<NanoGptUsage>> GetUsageAsync(
        string? from,
        string? to,
        string? groupBy,
        CancellationToken ct)
    {
        try
        {
            var query = new NanoGptUsageQuery(from, to, groupBy);
            var usage = await _account.GetUsageAsync(query, ct);
            return Result<NanoGptUsage>.Success(usage);
        }
        catch (Exception ex) when (ex is NanoGptAccountException or HttpRequestException or OperationCanceledException)
        {
            return MapFailure<NanoGptUsage>(ex, ct);
        }
    }

    public async Task<Result<NanoGptDepositLimits>> GetDepositLimitsAsync(CancellationToken ct)
    {
        try
        {
            var limits = await _account.GetBtcLnLimitsAsync(ct);
            return Result<NanoGptDepositLimits>.Success(limits);
        }
        catch (Exception ex) when (ex is NanoGptAccountException or HttpRequestException or OperationCanceledException)
        {
            return MapFailure<NanoGptDepositLimits>(ex, ct);
        }
    }

    public async Task<Result<NanoGptDeposit>> CreateDepositAsync(decimal amount, CancellationToken ct)
    {
        try
        {
            var deposit = await _account.CreateBtcLnDepositAsync(amount, ct);
            return Result<NanoGptDeposit>.Success(deposit);
        }
        catch (Exception ex) when (ex is NanoGptAccountException or HttpRequestException or OperationCanceledException)
        {
            return MapFailure<NanoGptDeposit>(ex, ct);
        }
    }

    public async Task<Result<NanoGptDeposit>> GetDepositAsync(string txId, CancellationToken ct)
    {
        try
        {
            var deposit = await _account.GetBtcLnDepositAsync(txId, ct);
            return Result<NanoGptDeposit>.Success(deposit);
        }
        catch (NanoGptAccountException ex) when ((int)ex.StatusCode == 404)
        {
            return Result<NanoGptDeposit>.Failure(new Error(
                ErrorKind.NotFound,
                "deposit_not_found",
                "Deposit was not found."));
        }
        catch (Exception ex) when (ex is NanoGptAccountException or HttpRequestException or OperationCanceledException)
        {
            return MapFailure<NanoGptDeposit>(ex, ct);
        }
    }

    private static Result<T> MapFailure<T>(Exception ex, CancellationToken ct)
    {
        if (ex is OperationCanceledException && ct.IsCancellationRequested)
        {
            throw ex;
        }

        if (ex is NanoGptAccountException nano)
        {
            return Result<T>.Failure(MapStatus(nano));
        }

        return Result<T>.Failure(new Error(
            ErrorKind.DependencyFailure,
            "nanogpt_unavailable",
            "NanoGPT is unavailable."));
    }

    private static Error MapStatus(NanoGptAccountException ex)
    {
        var status = (int)ex.StatusCode;
        if (status == 400)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? "Invalid request to NanoGPT."
                : ex.Message;
            return new Error(ErrorKind.Validation, "nanogpt_validation", message);
        }

        if (status == 401)
        {
            return new Error(ErrorKind.DependencyFailure, "nanogpt_auth", "NanoGPT authentication failed.");
        }

        if (status == 404)
        {
            return new Error(ErrorKind.NotFound, "nanogpt_not_found", "NanoGPT resource was not found.");
        }

        if (status == 429)
        {
            return new Error(ErrorKind.RateLimited, "nanogpt_rate_limited", "NanoGPT rate limit exceeded.");
        }

        return new Error(ErrorKind.DependencyFailure, "nanogpt_unavailable", "NanoGPT is unavailable.");
    }
}
