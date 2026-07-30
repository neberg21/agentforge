using System.Net.Http.Json;
using System.Text;
using AgentForge.Areas.Agents.Http;

namespace AgentForge.Host.Integration;

public sealed class RunStreamTests : IDisposable
{
    private readonly AgentForgeFactory _factory = AgentForgeFactory.ForRunExecution();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Stream_emits_status_and_done_events()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        var agent = await ApiClient.CreateAgentAsync(client, "Builder", ct);

        using var created = await client.PostAsJsonAsync(
            "/api/agents/runs",
            new CreateRunRequest(agent.Id, "Stream mich."),
            ct);
        created.EnsureSuccessStatusCode();
        var run = (await created.Content.ReadFromJsonAsync<RunResponse>(ct))!;

        using var streamResponse = await client.GetAsync(
            $"/api/agents/runs/{run.Id}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        streamResponse.EnsureSuccessStatusCode();

        await using var stream = await streamResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var events = new List<string>();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTime.UtcNow < deadline && events.Count < 20)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                events.Add(line["event:".Length..].Trim());
                if (events[^1] == "done")
                {
                    break;
                }
            }
        }

        Assert.Contains("status", events);
        Assert.Contains("done", events);
    }
}
