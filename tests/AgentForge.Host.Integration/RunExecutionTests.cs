using System.Net.Http.Json;
using AgentForge.Areas.Agents.Http;

namespace AgentForge.Host.Integration;

public sealed class RunExecutionTests : IDisposable
{
    private readonly AgentForgeFactory _factory = AgentForgeFactory.ForRunExecution();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Runs_WhenCreated_ReachCompletedWithAssistantAndTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Baue eine Todo-App."),
            ct);
        created.EnsureSuccessStatusCode();
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;
        Assert.Equal("Pending", run.Status);

        var completed = await PollUntilAsync(
            client,
            run.Id,
            status => status is "Completed" or "Failed" or "Cancelled",
            TimeSpan.FromSeconds(10),
            ct);

        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.PromptTokens);
        Assert.NotNull(completed.CompletionTokens);
        Assert.NotNull(completed.CostEstimate);

        var messages = await client.GetFromJsonAsync<RunMessageResponse[]>(
            $"/api/agents/runs/{run.Id}/messages",
            ct);
        Assert.Contains(messages!, m => m.Role == "Assistant" && m.Content == "OK");
    }

    [Fact]
    public async Task Runs_WhenCancelledWhileRunning_BecomeCancelled()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new AgentForge.Areas.Agents.Runtime.Llm.LlmCompletionResult(
            "Zu spaet.",
            [],
            new AgentForge.Areas.Agents.Runtime.Llm.LlmUsage(1, 1));
        var llm = new GateLlmClient(gate, result);

        using var factory = AgentForgeFactory.ForRunExecution(llm);
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Lang."),
            ct);
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        RunResponse? running = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var poll = await client.GetAsync($"/api/agents/runs/{run.Id}", ct);
            if (!poll.IsSuccessStatusCode)
            {
                var body = await poll.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"{(int)poll.StatusCode}: {body}");
            }

            running = await poll.Content.ReadFromJsonAsync<RunResponse>(ct);
            if (running!.Status == "Running")
            {
                break;
            }

            await Task.Delay(20, ct);
        }

        Assert.Equal("Running", running!.Status);

        using var cancelled = await client.PostAsJsonAsync(
            $"/api/agents/runs/{run.Id}/cancel",
            new CancelRunRequest(running.ConcurrencyToken),
            ct);
        cancelled.EnsureSuccessStatusCode();

        gate.TrySetResult();

        var final = await PollUntilAsync(
            client,
            run.Id,
            status => status is "Cancelled" or "Completed" or "Failed",
            TimeSpan.FromSeconds(10),
            ct);

        Assert.Equal("Cancelled", final.Status);
    }

    private static async Task<RunResponse> PollUntilAsync(
        HttpClient client,
        Guid runId,
        Func<string, bool> predicate,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        RunResponse? latest = null;

        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync($"/api/agents/runs/{runId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"{(int)response.StatusCode}: {body}");
            }

            latest = await response.Content.ReadFromJsonAsync<RunResponse>(ct);
            if (latest is not null && predicate(latest.Status))
            {
                return latest;
            }

            await Task.Delay(50, ct);
        }

        throw new TimeoutException($"Run {runId} did not reach expected status. Last={latest?.Status}");
    }
}
