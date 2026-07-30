# AgentForge — Agent-Runtime: Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Runs execute automatically after create: background worker, NanoGPT-compatible LLM loop with pluggable stub tools, token/cost tracking, SSE progress, and cancel while queued or running.

**Architecture:** Runtime stays inside `AgentForge.Areas.Agents` under `Runtime/`. `RunService.CreateAsync` enqueues after save. `RunWorker` (`BackgroundService` + `Channel` + semaphore) runs `RunLoop`, which calls `ILlmClient` and `IToolRegistry`, persists messages/usage, and publishes via `IRunEventBus`. Tests use a fake LLM; Host Testing environment registers the fake instead of HTTP.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, `HttpClient` for OpenAI-compatible chat completions, `System.Threading.Channels`, xUnit v3, existing EF Core SQLite persistence.

**Spec:** `docs/superpowers/specs/2026-07-30-agentforge-agent-runtime-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`. Paths relative to it; commands run from there.
- Follow existing Global Constraints from the skeleton plan: `net10.0`, CPM via `dotnet add package`, xUnit v3, no assertion/mocking libraries (hand-written fakes), `Guid.CreateVersion7()`, `IClock` for time, ProblemDetails for HTTP errors, English commit messages (`feat:` / `test:` / `chore:`).
- **No C# primary constructors.** Use traditional constructors with fields.
- **Do not inline object creation into method/ctor calls** — assign to a local, then pass the variable.
- **Windows:** no `.ps1` / `.sh`. Use `cmd /c` or direct `dotnet`/`git`. Commit messages via a temp file or `git commit -F`.
- Do not add new top-level directories beyond existing `src`, `tests`, `docs`.
- After each task: commit only that task's files.

## File Structure

**`src/Areas/AgentForge.Areas.Agents/Domain/`** (modify)
- `RunTransitions.cs` — open `Pending→Running`, `Running→Completed|Failed|Cancelled`
- `Run.cs` — `MarkRunning`, `Complete`, `Fail`, `ApplyUsage` (Cancel already exists)

**`src/Areas/AgentForge.Areas.Agents/Runtime/`** (create)
- `AgentsOptions.cs` — `Areas:Agents` binding
- `CostEstimator.cs` — tokens → `decimal` estimate
- `Llm/ILlmClient.cs`, `LlmCompletionRequest.cs`, `LlmCompletionResult.cs`, `LlmToolCall.cs`, `LlmUsage.cs`
- `Llm/OpenAiCompatibleLlmClient.cs` — HTTP implementation
- `Llm/FakeLlmClient.cs` — scripted/in-memory for Testing (also usable from unit tests via test project copy or shared test double)
- `Tools/ITool.cs`, `IToolRegistry.cs`, `ToolRegistry.cs`, `StubTool.cs`
- `Events/RunEvent.cs`, `IRunEventBus.cs`, `InProcessRunEventBus.cs`
- `Queue/IRunQueue.cs`, `ChannelRunQueue.cs`, `RunWorker.cs`
- `RunLoop.cs` — turn orchestration

**`src/Areas/AgentForge.Areas.Agents/Application/RunService.cs`** — enqueue after create

**`src/Areas/AgentForge.Areas.Agents/Http/RunEndpoints.cs`** — SSE `GET /runs/{id}/stream`

**`src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`** — options, DI, hosted worker, fake vs HTTP LLM by environment

**`src/AgentForge.Host/appsettings.json`**, `appsettings.Development.json` — `Areas:Agents` section

**Tests:** extend `AgentForge.Areas.Agents.Unit` and `AgentForge.Host.Integration`

---

### Task 1: Domain transitions and Run mutators

**Files:**
- Modify: `src/Areas/AgentForge.Areas.Agents/Domain/RunTransitions.cs`
- Modify: `src/Areas/AgentForge.Areas.Agents/Domain/Run.cs`
- Modify: `tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RunLifecycleTests.cs`

**Interfaces:**
- Consumes: existing `Run`, `RunStatus`, `RunTransitions`
- Produces: allowed transitions per spec; `Run.MarkRunning(DateTimeOffset now)`, `Complete(DateTimeOffset now)`, `Fail(string error, DateTimeOffset now)`, `ApplyUsage(int promptDelta, int completionDelta, decimal costEstimate)` — all bump `ConcurrencyToken` where status changes; `ApplyUsage` updates cumulative token fields and `CostEstimate` without requiring a status change

- [ ] **Step 1: Replace `RunTransitionsTests` with the new matrix**

Replace the contents of `tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs` with:

```csharp
namespace AgentForge.Areas.Agents.Unit;

public class RunTransitionsTests
{
    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Running)]
    [InlineData(RunStatus.Pending, RunStatus.Cancelled)]
    [InlineData(RunStatus.Running, RunStatus.Completed)]
    [InlineData(RunStatus.Running, RunStatus.Failed)]
    [InlineData(RunStatus.Running, RunStatus.Cancelled)]
    public void IsAllowed_WhenSupported_ReturnsTrue(RunStatus from, RunStatus to) =>
        Assert.True(RunTransitions.IsAllowed(from, to));

    [Theory]
    [InlineData(RunStatus.Pending, RunStatus.Completed)]
    [InlineData(RunStatus.Pending, RunStatus.Failed)]
    [InlineData(RunStatus.Pending, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Pending)]
    [InlineData(RunStatus.Running, RunStatus.Running)]
    [InlineData(RunStatus.Completed, RunStatus.Cancelled)]
    [InlineData(RunStatus.Failed, RunStatus.Running)]
    [InlineData(RunStatus.Cancelled, RunStatus.Pending)]
    public void IsAllowed_WhenUnsupported_ReturnsFalse(RunStatus from, RunStatus to) =>
        Assert.False(RunTransitions.IsAllowed(from, to));
}
```

- [ ] **Step 2: Write `RunLifecycleTests` (failing until mutators exist)**

```csharp
namespace AgentForge.Areas.Agents.Unit;

public class RunLifecycleTests
{
    private static AgentDefinition Definition() =>
        new("Builder", null, "Du bist hilfreich.", "some-model", 0.5, 2048, 10, []);

    private static Run NewRun(TestClock clock)
    {
        var agent = Agent.Create("owner-1", Definition(), clock.UtcNow);
        return Run.Create(agent, "Baue etwas.", clock.UtcNow);
    }

    [Fact]
    public void MarkRunning_setzt_Status_und_StartedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        var started = clock.Advance(TimeSpan.FromSeconds(1));

        run.MarkRunning(started);

        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(started, run.StartedAt);
    }

    [Fact]
    public void Complete_setzt_CompletedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);
        var done = clock.Advance(TimeSpan.FromSeconds(5));

        run.Complete(done);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(done, run.CompletedAt);
        Assert.Null(run.Error);
    }

    [Fact]
    public void Fail_setzt_Error_und_CompletedAt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);

        run.Fail("boom", clock.Advance(TimeSpan.FromSeconds(1)));

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("boom", run.Error);
    }

    [Fact]
    public void Cancel_aus_Running_ist_erlaubt()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);
        run.MarkRunning(clock.UtcNow);

        run.Cancel(clock.Advance(TimeSpan.FromSeconds(1)));

        Assert.Equal(RunStatus.Cancelled, run.Status);
    }

    [Fact]
    public void ApplyUsage_kumuliert_Tokens_und_setzt_Kosten()
    {
        var clock = TestClock.AtEpoch();
        var run = NewRun(clock);

        run.ApplyUsage(10, 20, 0.01m);
        run.ApplyUsage(5, 7, 0.02m);

        Assert.Equal(15, run.PromptTokens);
        Assert.Equal(27, run.CompletionTokens);
        Assert.Equal(0.02m, run.CostEstimate);
    }
}
```

- [ ] **Step 3: Run tests — expect FAIL**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit --filter "FullyQualifiedName~RunTransitions|FullyQualifiedName~RunLifecycle"`
Expected: FAIL — `MarkRunning` missing and/or transition assertions wrong.

- [ ] **Step 4: Update `RunTransitions`**

```csharp
namespace AgentForge.Areas.Agents.Domain;

public static class RunTransitions
{
    private static readonly Dictionary<RunStatus, RunStatus[]> Allowed = new()
    {
        [RunStatus.Pending] = [RunStatus.Running, RunStatus.Cancelled],
        [RunStatus.Running] = [RunStatus.Completed, RunStatus.Failed, RunStatus.Cancelled],
        [RunStatus.Completed] = [],
        [RunStatus.Failed] = [],
        [RunStatus.Cancelled] = []
    };

    public static bool IsAllowed(RunStatus from, RunStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
```

- [ ] **Step 5: Add mutators on `Run`**

Append to `Run` (keep existing members). Use traditional methods; throw `InvalidOperationException` on illegal transitions (same style as `Cancel`):

```csharp
    public void MarkRunning(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Running))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Running}.");
        }

        Status = RunStatus.Running;
        StartedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Complete(DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Completed))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Completed}.");
        }

        Status = RunStatus.Completed;
        CompletedAt = now;
        Error = null;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void Fail(string error, DateTimeOffset now)
    {
        if (!CanTransitionTo(RunStatus.Failed))
        {
            throw new InvalidOperationException($"A run in status {Status} cannot move to {RunStatus.Failed}.");
        }

        Status = RunStatus.Failed;
        Error = error;
        CompletedAt = now;
        ConcurrencyToken = Guid.CreateVersion7();
    }

    public void ApplyUsage(int promptDelta, int completionDelta, decimal costEstimate)
    {
        PromptTokens = (PromptTokens ?? 0) + promptDelta;
        CompletionTokens = (CompletionTokens ?? 0) + completionDelta;
        CostEstimate = costEstimate;
    }
```

- [ ] **Step 6: Run tests — expect PASS**

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit --filter "FullyQualifiedName~RunTransitions|FullyQualifiedName~RunLifecycle"`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add src/Areas/AgentForge.Areas.Agents/Domain/RunTransitions.cs src/Areas/AgentForge.Areas.Agents/Domain/Run.cs tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs tests/AgentForge.Areas.Agents.Unit/RunLifecycleTests.cs
git commit -m "feat: open run state machine for runtime lifecycle"
```

---

### Task 2: AgentsOptions and cost estimator

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs`
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/CostEstimator.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/CostEstimatorTests.cs`
- Modify: `src/AgentForge.Host/appsettings.json`, `appsettings.Development.json`

**Interfaces:**
- Produces: `AgentsOptions` with nested `Llm` (`BaseUrl`, `ApiKey`, `Timeout`), `MaxConcurrentRuns` (default 2, min 1), `Pricing` (`PromptTokenPerMillion`, `CompletionTokenPerMillion`); section name `Areas:Agents`. `CostEstimator.Estimate(promptTokens, completionTokens, pricing) → decimal` using `(prompt/1e6)*promptPrice + (completion/1e6)*completionPrice`

- [ ] **Step 1: Failing cost tests**

```csharp
using AgentForge.Areas.Agents.Runtime;

namespace AgentForge.Areas.Agents.Unit;

public class CostEstimatorTests
{
    [Fact]
    public void Estimate_rechnet_anteilig_pro_Million()
    {
        var pricing = new AgentsPricingOptions
        {
            PromptTokenPerMillion = 1.0m,
            CompletionTokenPerMillion = 2.0m
        };

        var estimate = CostEstimator.Estimate(500_000, 250_000, pricing);

        Assert.Equal(1.0m, estimate);
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (types missing)

- [ ] **Step 3: Implement options + estimator**

`AgentsOptions.cs` — DataAnnotations on required strings and ranges; nested classes `AgentsLlmOptions`, `AgentsPricingOptions`. Properties: `Llm`, `MaxConcurrentRuns`, `Pricing`. Constant `SectionName = "Areas:Agents"`.

`CostEstimator` — static `Estimate` as above; round to 6 decimal places with `MidpointRounding.AwayFromZero` if needed for assert stability.

- [ ] **Step 4: Add config defaults to Host appsettings**

Under root JSON add (Development may override ApiKey via user-secrets later; put a placeholder empty key and a dummy BaseUrl for structure):

```json
  "Areas": {
    "Agents": {
      "Llm": {
        "BaseUrl": "https://nano-gpt.com/api/v1",
        "ApiKey": "",
        "Timeout": "00:01:00"
      },
      "MaxConcurrentRuns": 2,
      "Pricing": {
        "PromptTokenPerMillion": 0.50,
        "CompletionTokenPerMillion": 1.50
      }
    }
  }
```

Wire binding in Task 7 (`AgentsArea`); this task only adds types + appsettings + unit test.

- [ ] **Step 5: Tests PASS, commit**

```bash
git commit -m "feat: agents runtime options and cost estimator"
```

---

### Task 3: LLM contracts and fake client

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Llm/ILlmClient.cs` and related records
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Llm/ScriptedLlmClient.cs` (queue of scripted results for tests; also registered in Testing as singleton/factory)
- Test: `tests/AgentForge.Areas.Agents.Unit/ScriptedLlmClientTests.cs`

**Interfaces:**
- Produces:
  - `record LlmToolCall(string Id, string Name, string ArgumentsJson)`
  - `record LlmUsage(int PromptTokens, int CompletionTokens)`
  - `record LlmMessage(string Role, string? Content, string? ToolCallsJson, string? ToolCallId)` — roles: `system|user|assistant|tool` lowercase for wire format helpers
  - `sealed class LlmCompletionRequest` with `Model`, `Temperature`, `MaxOutputTokens`, `IReadOnlyList<LlmMessage> Messages`, `IReadOnlyList<string> AllowedToolNames`
  - `sealed class LlmCompletionResult` with `string? Content`, `IReadOnlyList<LlmToolCall> ToolCalls`, `LlmUsage Usage`
  - `interface ILlmClient { Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken ct); }`
  - `ScriptedLlmClient` — constructor takes `IEnumerable<LlmCompletionResult>` or `Enqueue`; dequeues one result per call; throws if empty

- [ ] **Step 1–5:** TDD ScriptedLlmClient returns enqueued results in order; commit `feat: llm client contract and scripted fake`

---

### Task 4: Tool registry and stubs

**Files:**
- Create: `Runtime/Tools/ITool.cs`, `IToolRegistry.cs`, `ToolRegistry.cs`, `StubTool.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/ToolRegistryTests.cs`

**Interfaces:**
- `interface ITool { string Name { get; } Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct); }`
- `StubTool(string name)` returns JSON like `{"ok":true,"tool":"<name>","note":"stub"}`
- `ToolRegistry` : `void Register(ITool tool)`, `ITool? Find(string name)`, `void EnsureStubs(IEnumerable<string> names)` — registers `StubTool` for each missing name
- Unknown tool: loop will not call Find null blindly — registry method `ExecuteOrErrorAsync(name, args, ct)` returns stub error JSON `{"ok":false,"error":"unknown_tool","tool":"..."}` when missing

- [ ] TDD + commit `feat: pluggable tool registry with stubs`

---

### Task 5: In-process run event bus

**Files:**
- Create: `Runtime/Events/RunEvent.cs`, `IRunEventBus.cs`, `InProcessRunEventBus.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RunEventBusTests.cs`

**Interfaces:**
- `enum RunEventType { Status, Message, Usage, Error, Done }`
- `sealed record RunEvent(Guid RunId, RunEventType Type, string JsonPayload)`
- `IRunEventBus`: `void Publish(RunEvent ev)`, `IAsyncEnumerable<RunEvent> Subscribe(Guid runId, CancellationToken ct)` — subscriber receives events for that runId after subscription (no replay)
- Implementation: `Channel`-per-subscriber list under lock; Publish writes to all matching subscribers; completed/done may complete the channel

- [ ] TDD: publish after subscribe delivers; wrong runId ignored; commit `feat: in-process run event bus for sse`

---

### Task 6: RunLoop

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/RunLoop.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RunLoopTests.cs`

**Interfaces:**
- Consumes: `AgentsDbContext`, `ILlmClient`, `IToolRegistry`, `IRunEventBus`, `IClock`, `IOptions<AgentsOptions>` (for pricing)
- Produces: `RunLoop.ExecuteAsync(Guid runId, CancellationToken ct)` 
  - Load run with messages (ignore query filter issues by using same owner context as worker — worker creates scope with system owner: see Task 7). For unit tests, use `AgentsDatabase` + set owner to run.OwnerId.
  - If status is `Cancelled` or not `Pending`, return without work (idempotent).
  - `MarkRunning`, save, publish status.
  - Loop while turns < MaxTurns: build `LlmCompletionRequest` from messages + snapshot; call LLM; append assistant message; apply usage + cost; publish; if tool calls, execute each via registry, append tool messages, publish; if no tool calls, `Complete`, publish done, return; check cancel between steps (reload status or honor ct + DB status Cancelled).
  - On LLM exception: `Fail`, publish error+done.
  - On max turns: `Fail` with message containing `max_turns`.

**Unit tests (ScriptedLlmClient + AgentsDatabase):**
1. Single assistant reply without tools → Completed, tokens set, cost > 0 when pricing non-zero.
2. Assistant tool_call then final assistant → tool message present, Completed.
3. Script throws → Failed.
4. MaxTurns=1 with perpetual tool_calls script → Failed max_turns.
5. Cancel after MarkRunning (second context sets Cancel) before next LLM call → Cancelled.

Map domain messages to LLM roles: System/User/Assistant/Tool → lowercase. Persist `ToolCallsJson` as JSON array of `{id,type:"function",function:{name,arguments}}` OpenAI shape for round-trip simplicity — document the exact string in the loop helper.

- [ ] TDD RED/GREEN + commit `feat: run loop with llm turns tools usage and cancel`

---

### Task 7: Queue, worker, DI, OpenAI client, enqueue on create

**Files:**
- Create: `Runtime/Queue/IRunQueue.cs`, `ChannelRunQueue.cs`, `RunWorker.cs`
- Create: `Runtime/Llm/OpenAiCompatibleLlmClient.cs`
- Modify: `Application/RunService.cs`, `AgentsArea.cs`
- Test: extend unit tests for queue if useful; integration deferred to Task 8

**Interfaces:**
- `IRunQueue.Enqueue(Guid runId)` — non-blocking write to unbounded/bounded channel
- `ChannelRunQueue` singleton
- `RunWorker` : `BackgroundService` — read channel, `SemaphoreSlim(MaxConcurrentRuns)`, create scope, resolve `RunLoop` + `ICurrentUser` problem: DbContext filters by owner. **Resolution:** worker loads run id without filter using a dedicated internal method OR temporarily use `IgnoreQueryFilters()` in `RunLoop` when executing by id, then assert ownership is irrelevant for single-tenant local user. Prefer `RunLoop` query: `db.Runs.IgnoreQueryFilters().Include(r => r.Messages).FirstOrDefaultAsync(r => r.Id == runId)`. Messages navigation must load — configure Include on field `_messages` via `db.Entry(run).Collection(...).LoadAsync` or Include. After load, if null return; if Cancelled skip; else execute.
- `RunService.CreateAsync` after successful save: `_queue.Enqueue(run.Id)`
- `AgentsArea.ConfigureServices`:
  - Bind `AgentsOptions` from `Areas:Agents`, `ValidateDataAnnotations`, `ValidateOnStart`, validate `MaxConcurrentRuns >= 1`
  - Register event bus singleton, queue singleton, tools registry (scoped or singleton — **singleton** registry with stubs created per run via EnsureStubs in loop is fine)
  - If `IHostEnvironment.IsEnvironment("Testing")` OR config `Areas:Agents:Llm:UseFake` true → register `ScriptedLlmClient` as `ILlmClient` (integration tests replace with their own via `ConfigureTestServices`). Default Testing: register a `ScriptedLlmClient` that always returns a fixed final assistant message `"OK"` with usage 1/1 so integration tests complete without custom setup; Host.Integration may replace via factory.
  - Else: `AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>` with BaseAddress from options; require non-empty ApiKey on start validate
  - `AddHostedService<RunWorker>()`
  - `AddScoped<RunLoop>()`

**OpenAiCompatibleLlmClient:** POST `{BaseUrl}/chat/completions` with Bearer key; map tools as function tools from AllowedToolNames (empty parameters schema `{}`); parse first choice message content + tool_calls; map usage. On non-success HTTP throw `HttpRequestException` with status.

- [ ] Implement; unit-test enqueue is called (hand-written recording queue fake injected into RunService test) 
- [ ] `dotnet test tests/AgentForge.Areas.Agents.Unit` PASS
- [ ] Commit `feat: run worker queue and openai llm client wiring`

---

### Task 8: SSE endpoint and integration tests

**Files:**
- Modify: `Http/RunEndpoints.cs`
- Modify: `tests/AgentForge.Host.Integration/AgentForgeFactory.cs` — register `ScriptedLlmClient` with desired scripts via `ConfigureTestServices`
- Create: `tests/AgentForge.Host.Integration/RunExecutionTests.cs`
- Create: `tests/AgentForge.Host.Integration/RunStreamTests.cs`

**SSE mapping:**

```csharp
group.MapGet("/{id:guid}/stream", async (
    Guid id,
    RunService runs,
    IRunEventBus bus,
    HttpResponse response,
    CancellationToken ct) =>
{
    var existing = await runs.GetAsync(id, ct);
    if (!existing.IsSuccess)
    {
        return existing.Error!.Value.ToProblem();
    }

    response.Headers.ContentType = "text/event-stream";
    await response.StartAsync(ct);

    await foreach (var ev in bus.Subscribe(id, ct))
    {
        await response.WriteAsync($"event: {ev.Type.ToString().ToLowerInvariant()}\n", ct);
        await response.WriteAsync($"data: {ev.JsonPayload}\n\n", ct);
        await response.Body.FlushAsync(ct);
        if (ev.Type == RunEventType.Done)
        {
            break;
        }
    }

    return Results.Empty;
});
```

(Adjust to compile against Minimal APIs — may use `TypedResults` / raw delegate returning `Task`.)

**Integration tests:**
1. Create agent + run → poll GET until status Completed (timeout ~10s) → messages include Assistant; tokens not null.
2. Subscribe SSE in parallel with create (or immediately after) → observe status and done events.
3. Create run with scripted client delayed; cancel while Running → Cancelled.
4. Existing RunEndpointTests still pass (Pending create still 201; note: run may complete quickly — assertions on Pending-only fields in create response remain valid if response is built before worker finishes; if flaky, assert create returns Pending in body even if later Completed).

Factory: remove/replace `ILlmClient` with `ScriptedLlmClient` that returns one assistant completion. Ensure `Areas:Agents` config present in in-memory configuration.

- [ ] PASS `dotnet test`
- [ ] Commit `feat: run sse stream and execution integration coverage`

---

### Task 9: README and completion criteria

**Files:**
- Modify: `README.md` — document `Areas:Agents`, fake vs real LLM, that runs execute asynchronously
- Run: `dotnet clean`, `dotnet build`, `dotnet test`
- Manual smoke optional: `dotnet run --project src/AgentForge.Host` with fake or key

**Checklist against spec Fertigstellungskriterien** (record in commit message body or README Stand section update):

1. build/test clean  
2. create auto-starts execution  
3. transitions + cancel from Pending/Running  
4. messages + tokens + cost  
5. SSE + polling  
6. options bound  
7. architecture tests green  

- [ ] Commit `docs: document agent runtime configuration and status`

---

## Spec coverage (self-review)

| Spec item | Task |
|---|---|
| Runtime in Agents area | 2–7 |
| ITool + stubs | 4 |
| Auto-queue on create | 7 |
| SSE + polling | 8 |
| Fake LLM in tests | 3, 7, 8 |
| MaxConcurrentRuns | 2, 7 |
| Transitions + mutators | 1 |
| Turn loop / max turns / cost | 2, 6 |
| OpenAI HTTP client | 7 |
| Options ValidateOnStart | 7 |
| Completion criteria / README | 9 |

No TBD placeholders. Types named consistently: `ILlmClient`, `RunLoop`, `IRunQueue`, `IRunEventBus`, `AgentsOptions`.
