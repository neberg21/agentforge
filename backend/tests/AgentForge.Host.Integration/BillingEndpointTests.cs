using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Host.Integration;

public class BillingEndpointTests : IClassFixture<AgentForgeFactory>
{
    private readonly AgentForgeFactory _factory;

    public BillingEndpointTests(AgentForgeFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Balance_WhenRequested_ReturnsOkWithThreshold()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/agents/billing/balance",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(doc.RootElement.TryGetProperty("usdBalance", out _));
        Assert.True(doc.RootElement.TryGetProperty("lowBalance", out _));
        Assert.Equal(5, doc.RootElement.GetProperty("lowBalanceThresholdUsd").GetDecimal());
    }

    [Fact]
    public async Task Deposits_WhenCreated_CanBeFetched()
    {
        using var client = _factory.CreateClient();
        var body = new { amount = 0.00002m };
        using var create = await client.PostAsJsonAsync(
            "/api/agents/billing/deposits",
            body,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var txId = created.GetProperty("txId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(txId));

        using var get = await client.GetAsync(
            $"/api/agents/billing/deposits/{txId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Usage_WhenRequested_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/agents/billing/usage",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DepositLimits_WhenRequested_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/agents/billing/deposits/limits",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
