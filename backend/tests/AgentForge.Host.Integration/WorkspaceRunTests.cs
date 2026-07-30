using System.Net.Http.Json;
using AgentForge.Areas.Agents.Http;
using AgentForge.Areas.Agents.Runtime.Llm;

namespace AgentForge.Host.Integration;

public sealed class WorkspaceRunTests : IDisposable
{
    private readonly string _root;
    private readonly RecordingGitWorkspace _git;
    private readonly AgentForgeFactory _factory;

    public WorkspaceRunTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ws-int-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var localPath = Path.Combine(_root, "clone");
        var worktreesRoot = Path.Combine(_root, "worktrees");

        _git = new RecordingGitWorkspace();
        var result = new LlmCompletionResult("OK", [], new LlmUsage(1, 1));
        var llm = new DelayedScriptedLlmClient([result], TimeSpan.FromMilliseconds(100));
        var configuration = new Dictionary<string, string?>
        {
            ["Areas:Agents:Workspace:Enabled"] = "true",
            ["Areas:Agents:Workspace:RemoteUrl"] = "https://example.invalid/repo.git",
            ["Areas:Agents:Workspace:LocalPath"] = localPath,
            ["Areas:Agents:Workspace:WorktreesRoot"] = worktreesRoot,
            ["Areas:Agents:Workspace:BaseRef"] = "main"
        };

        _factory = AgentForgeFactory.ForWorkspaceRun(llm, _git, configuration);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task Runs_WhenWorkspaceEnabled_PushesThenCompletesAndCleansUp()
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

        var completed = await PollUntilAsync(
            client,
            run.Id,
            status => status is "Completed" or "Failed" or "Cancelled",
            TimeSpan.FromSeconds(15),
            ct);

        Assert.Equal("Completed", completed.Status);
        Assert.Contains(_git.Calls, call => call.StartsWith("PushBranch:", StringComparison.Ordinal));
        Assert.Contains(_git.Calls, call => call.StartsWith("RemoveWorktree:", StringComparison.Ordinal));
        Assert.Equal(1, _git.Calls.Count(call => call.StartsWith("PushBranch:", StringComparison.Ordinal)));
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
            response.EnsureSuccessStatusCode();
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
