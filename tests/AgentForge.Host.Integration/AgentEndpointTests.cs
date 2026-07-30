namespace AgentForge.Host.Integration;

public sealed class AgentEndpointTests : IDisposable
{
    private readonly AgentForgeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Ein_Agent_durchlaeuft_Anlegen_Lesen_Aendern_Listen_und_Archivieren()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var created = await client.PostAsJsonAsync("/api/agents/definitions", ApiClient.NewAgent(), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var agent = (await created.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
        Assert.Equal("Builder", agent.Name);
        Assert.Equal(["read_file"], agent.AllowedTools);

        var fetched = await client.GetFromJsonAsync<AgentResponse>($"/api/agents/definitions/{agent.Id}", ct);
        Assert.Equal(agent.Id, fetched!.Id);

        var update = new UpdateAgentRequest("Umbenannt", null, "Neuer Prompt.", "other-model", 1.0, 512, 5, [], agent.ConcurrencyToken);
        using var updated = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var afterUpdate = (await updated.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
        Assert.Equal("Umbenannt", afterUpdate.Name);
        Assert.NotEqual(agent.ConcurrencyToken, afterUpdate.ConcurrencyToken);

        var listed = await client.GetFromJsonAsync<PagedResponse<AgentResponse>>("/api/agents/definitions", ct);
        Assert.Equal(1, listed!.Total);

        using var archived = await client.DeleteAsync($"/api/agents/definitions/{agent.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var afterArchive = await client.GetFromJsonAsync<PagedResponse<AgentResponse>>("/api/agents/definitions", ct);
        Assert.Equal(0, afterArchive!.Total);

        var stillReachable = await client.GetFromJsonAsync<AgentResponse>($"/api/agents/definitions/{agent.Id}", ct);
        Assert.NotNull(stillReachable!.ArchivedAt);
    }

    [Fact]
    public async Task Ein_unbekannter_Agent_ergibt_404_als_ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/agents/definitions/{Guid.CreateVersion7()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("agent_not_found", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Ein_leerer_Name_ergibt_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/definitions",
            ApiClient.NewAgent() with { Name = "" },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Eine_unzulaessige_Temperatur_ergibt_400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/definitions",
            ApiClient.NewAgent() with { Temperature = 5.0 },
            ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ein_doppelter_Name_ergibt_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var response = await client.PostAsJsonAsync("/api/agents/definitions", ApiClient.NewAgent(), ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("agent_name_taken", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Ein_veraltetes_Token_ergibt_409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        var update = new UpdateAgentRequest("Umbenannt", null, "Prompt.", "some-model", 0.5, 2048, 10, [], Guid.CreateVersion7());
        using var response = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency_conflict", await ApiClient.ReadErrorCodeAsync(response, ct));
    }
}
