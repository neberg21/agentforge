namespace AgentForge.Host.Integration;

public sealed class RunEndpointTests : IDisposable
{
    private readonly AgentForgeFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Runs_WhenCreated_ArePendingWithTwoMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        Assert.Equal("Pending", run.Status);
        Assert.Equal("Du bist hilfreich.", run.AgentSnapshot.SystemPrompt);
        Assert.Null(run.StartedAt);
        Assert.Null(run.CompletedAt);

        var messages = await client.GetFromJsonAsync<RunMessageResponse[]>($"/api/agents/runs/{run.Id}/messages", ct);
        Assert.Equal(["System", "User"], messages!.Select(m => m.Role));
        Assert.Equal("Baue eine Todo-App.", messages![1].Content);
    }

    [Fact]
    public async Task Runs_WhenAgentUpdated_KeepFrozenSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        var update = new UpdateAgentRequest("Builder", null, "Voellig anderer Prompt.", "other-model", 1.0, 512, 5, [], agent.ConcurrencyToken);
        using var updated = await client.PutAsJsonAsync($"/api/agents/definitions/{agent.Id}", update, ct);
        updated.EnsureSuccessStatusCode();

        var reloaded = await client.GetFromJsonAsync<RunResponse>($"/api/agents/runs/{run.Id}", ct);

        Assert.Equal("Du bist hilfreich.", reloaded!.AgentSnapshot.SystemPrompt);
        Assert.Equal("some-model", reloaded.AgentSnapshot.Model);
    }

    [Fact]
    public async Task Runs_WhenCancelledTwice_SecondReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        using var cancelled = await client.PostAsJsonAsync(
            $"/api/agents/runs/{run.Id}/cancel",
            new CancelRunRequest(run.ConcurrencyToken),
            ct);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var afterCancel = (await cancelled.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        Assert.Equal("Cancelled", afterCancel.Status);
        Assert.NotNull(afterCancel.CompletedAt);

        using var again = await client.PostAsJsonAsync(
            $"/api/agents/runs/{run.Id}/cancel",
            new CancelRunRequest(afterCancel.ConcurrencyToken),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("run_invalid_transition", await ApiClient.ReadErrorCodeAsync(again, ct));
    }

    [Fact]
    public async Task Runs_WhenAgentArchived_RejectNewRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var archived = await client.DeleteAsync($"/api/agents/definitions/{agent.Id}", ct);
        archived.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Zu spaet."),
            ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("agent_archived", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Runs_WhenUnknown_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/agents/runs/{Guid.CreateVersion7()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("run_not_found", await ApiClient.ReadErrorCodeAsync(response, ct));
    }

    [Fact]
    public async Task Runs_WhenFiltered_RespectAgentAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var first = await ApiClient.CreateAgentAsync(client, "Alpha", ct);
        var second = await ApiClient.CreateAgentAsync(client, "Bravo", ct);

        using var one = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(first.Id, "Eins."), ct);
        using var two = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(first.Id, "Zwei."), ct);
        using var three = await client.PostAsJsonAsync("/api/agents/runs", new CreateRunRequest(second.Id, "Drei."), ct);
        one.EnsureSuccessStatusCode();
        three.EnsureSuccessStatusCode();

        var toCancel = (await two.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        using var cancelled = await client.PostAsJsonAsync(
            $"/api/agents/runs/{toCancel.Id}/cancel",
            new CancelRunRequest(toCancel.ConcurrencyToken),
            ct);
        cancelled.EnsureSuccessStatusCode();

        var byAgent = await client.GetFromJsonAsync<PagedResponse<RunResponse>>($"/api/agents/runs?agentId={first.Id}", ct);
        var byStatus = await client.GetFromJsonAsync<PagedResponse<RunResponse>>("/api/agents/runs?status=Pending", ct);

        Assert.Equal(2, byAgent!.Total);
        Assert.Equal(2, byStatus!.Total);
        Assert.DoesNotContain(toCancel.Id, byStatus.Items.Select(r => r.Id));
    }
}
