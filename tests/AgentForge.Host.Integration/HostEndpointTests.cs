namespace AgentForge.Host.Integration;

public class HostEndpointTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public async Task Health_WhenLivenessRequested_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_WhenReadinessRequested_ReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Areas_WhenListed_ReturnsRegisteredSlugs()
    {
        using var client = factory.CreateClient();

        var areas = await client.GetFromJsonAsync<AreaInfo[]>("/api/areas", TestContext.Current.CancellationToken);

        Assert.NotNull(areas);
        Assert.Equal(["agents"], areas.Select(a => a.Slug));
    }

    [Fact]
    public async Task UnknownPath_WhenRequested_ReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/gibt-es-nicht", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
