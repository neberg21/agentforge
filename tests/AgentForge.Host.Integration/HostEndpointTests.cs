namespace AgentForge.Host.Integration;

public class HostEndpointTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public async Task Liveness_antwortet_mit_200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_antwortet_mit_200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/_health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Areas_liefert_die_registrierten_Bereiche()
    {
        using var client = factory.CreateClient();

        var areas = await client.GetFromJsonAsync<AreaInfo[]>("/api/areas", TestContext.Current.CancellationToken);

        Assert.NotNull(areas);
        Assert.Empty(areas);
    }

    [Fact]
    public async Task Unbekannter_Pfad_antwortet_mit_404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/gibt-es-nicht", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
