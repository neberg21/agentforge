using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Billing;
using AgentForge.Core;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class BillingServiceTests
{
    private sealed class StubAccountClient : INanoGptAccountClient
    {
        public Func<CancellationToken, Task<NanoGptBalance>> BalanceAsync { get; set; } =
            _ => Task.FromResult(new NanoGptBalance(12m, 0m, null));

        public Func<decimal, CancellationToken, Task<NanoGptDeposit>> CreateDepositAsyncFn { get; set; } =
            (amount, _) => Task.FromResult(new NanoGptDeposit(
                "tx", amount, "New", null, null, null, null));

        public Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct) => BalanceAsync(ct);

        public Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct) =>
            CreateDepositAsyncFn(amount, ct);

        public Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private static BillingService CreateService(
        INanoGptAccountClient account,
        decimal threshold = 5m)
    {
        var options = new AgentsOptions
        {
            Llm = new AgentsLlmOptions { BaseUrl = "https://nano-gpt.com/api/v1" },
            Pricing = new AgentsPricingOptions(),
            Billing = new AgentsBillingOptions { LowBalanceUsdThreshold = threshold }
        };
        var wrapped = Options.Create(options);
        return new BillingService(account, wrapped);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenBelowThreshold_SetsLowBalance()
    {
        var account = new StubAccountClient
        {
            BalanceAsync = _ => Task.FromResult(new NanoGptBalance(3m, 0m, null))
        };
        var service = CreateService(account, threshold: 5m);

        var result = await service.GetBalanceAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.LowBalance);
        Assert.Equal(5m, result.Value.LowBalanceThresholdUsd);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenUnauthorized_ReturnsDependencyFailure()
    {
        var account = new StubAccountClient
        {
            BalanceAsync = _ => throw new NanoGptAccountException(
                System.Net.HttpStatusCode.Unauthorized,
                "nope")
        };
        var service = CreateService(account);

        var result = await service.GetBalanceAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.DependencyFailure, result.Error!.Value.Kind);
        Assert.Equal("nanogpt_auth", result.Error.Value.Code);
    }

    [Fact]
    public async Task CreateDepositAsync_WhenAmountRejected_ReturnsValidation()
    {
        var account = new StubAccountClient
        {
            CreateDepositAsyncFn = (_, _) => throw new NanoGptAccountException(
                System.Net.HttpStatusCode.BadRequest,
                "Minimum amount is X")
        };
        var service = CreateService(account);

        var result = await service.CreateDepositAsync(0.0000001m, CancellationToken.None);

        Assert.Equal(ErrorKind.Validation, result.Error!.Value.Kind);
    }
}
