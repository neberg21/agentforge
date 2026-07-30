using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class RunLoopTests
{
    private static AgentDefinition Definition(int maxTurns = 10, string[]? tools = null) =>
        new("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, maxTurns, tools ?? []);

    private static AgentsOptions CreateOptions() =>
        new()
        {
            Pricing =
            {
                PromptTokenPerMillion = 1.0m,
                CompletionTokenPerMillion = 2.0m
            }
        };

    private static RunLoop CreateLoop(
        AgentsDatabase database,
        ILlmClient llm,
        IRunEventBus? bus = null,
        AgentsOptions? agentsOptions = null)
    {
        var context = database.NewContext();
        var tools = new ToolRegistry();
        var events = bus ?? new InProcessRunEventBus();
        var clock = TestClock.AtEpoch();
        var options = Options.Create(agentsOptions ?? CreateOptions());
        return new RunLoop(context, llm, tools, events, clock, options);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkspaceEnabled_LeavesRunningOnNaturalSuccess()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = new LlmCompletionResult("Fertig.", [], new LlmUsage(10, 20));
        var llm = new ScriptedLlmClient([result]);
        var options = CreateOptions();
        options.Workspace.Enabled = true;
        var loop = CreateLoop(database, llm, agentsOptions: options);

        await loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var loaded = await verify.Runs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RunStatus.Running, loaded.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAssistantRepliesWithoutTools_Completes()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = new LlmCompletionResult("Fertig.", [], new LlmUsage(10, 20));
        var llm = new ScriptedLlmClient([result]);
        var loop = CreateLoop(database, llm);

        await loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var loaded = await verify.Runs
            .Include(r => r.Messages)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RunStatus.Completed, loaded.Status);
        Assert.Equal(10, loaded.PromptTokens);
        Assert.Equal(20, loaded.CompletionTokens);
        Assert.NotNull(loaded.CostEstimate);
        Assert.Contains(loaded.Messages, m => m.Role == MessageRole.Assistant && m.Content == "Fertig.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolCallThenFinalReply_CompletesWithToolMessage()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(
            database.CurrentUser.OwnerId,
            Definition(tools: ["read_file"]),
            TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var toolCall = new LlmToolCall("call-1", "read_file", "{}");
        var withTools = new LlmCompletionResult(null, [toolCall], new LlmUsage(5, 5));
        var final = new LlmCompletionResult("Done.", [], new LlmUsage(5, 5));
        var llm = new ScriptedLlmClient([withTools, final]);
        var loop = CreateLoop(database, llm);

        await loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var loaded = await verify.Runs
            .Include(r => r.Messages)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RunStatus.Completed, loaded.Status);
        Assert.Contains(loaded.Messages, m => m.Role == MessageRole.Tool && m.ToolCallId == "call-1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenLlmThrows_Fails()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var llm = new ThrowingLlmClient();
        var loop = CreateLoop(database, llm);

        await loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var loaded = await verify.Runs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RunStatus.Failed, loaded.Status);
        Assert.Equal("boom", loaded.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxTurnsExceeded_FailsWithMaxTurns()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(
            database.CurrentUser.OwnerId,
            Definition(maxTurns: 1, tools: ["read_file"]),
            TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var toolCall = new LlmToolCall("call-1", "read_file", "{}");
        var withTools = new LlmCompletionResult(null, [toolCall], new LlmUsage(1, 1));
        var llm = new ScriptedLlmClient([withTools, withTools]);
        var loop = CreateLoop(database, llm);

        await loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        await using var verify = database.NewContext();
        var loaded = await verify.Runs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RunStatus.Failed, loaded.Status);
        Assert.Contains("max_turns", loaded.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringLlm_EndsCancelled()
    {
        using var database = new AgentsDatabase();
        var agent = Agent.Create(database.CurrentUser.OwnerId, Definition(), TestClock.AtEpoch().UtcNow);
        var run = Run.Create(agent, "Los.", TestClock.AtEpoch().UtcNow);

        await using (var context = database.NewContext())
        {
            context.Agents.Add(agent);
            context.Runs.Add(run);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new LlmCompletionResult("Zu spaet.", [], new LlmUsage(1, 1));
        var llm = new WaitingLlmClient(gate, result);
        var loop = CreateLoop(database, llm);

        var execute = loop.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var poll = database.NewContext();
            var status = await poll.Runs
                .AsNoTracking()
                .Select(candidate => candidate.Status)
                .SingleAsync(TestContext.Current.CancellationToken);
            if (status == RunStatus.Running)
            {
                break;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await using (var cancelContext = database.NewContext())
        {
            var tracked = await cancelContext.Runs.SingleAsync(TestContext.Current.CancellationToken);
            tracked.Cancel(TestClock.AtEpoch().UtcNow);
            await cancelContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        gate.SetResult();
        await execute;

        await using var verify = database.NewContext();
        var loaded = await verify.Runs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RunStatus.Cancelled, loaded.Status);
    }

    private sealed class ThrowingLlmClient : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class WaitingLlmClient : ILlmClient
    {
        private readonly TaskCompletionSource _gate;
        private readonly LlmCompletionResult _result;

        public WaitingLlmClient(TaskCompletionSource gate, LlmCompletionResult result)
        {
            _gate = gate;
            _result = result;
        }

        public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct)
        {
            await _gate.Task.WaitAsync(ct);
            return _result;
        }
    }
}
