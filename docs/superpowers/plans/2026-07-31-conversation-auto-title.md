# Conversation Auto-Title Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Editable conversation titles with background LLM suggestions when created blank, live SSE updates every completed-turn cadence, and OK / Auto lock-pause-resume controls.

**Architecture:** Persist `TitleMode` + turn bookkeeping on `Conversation`. After each completed reply turn, enqueue a dedicated title job (deduped) that calls a fixed cheap model and publishes SSE `title` events. Frontend editable header with OK/Auto; reconnect the conversation EventSource after `done` so late title events still arrive.

**Tech Stack:** .NET 10 / EF Core / xUnit v3 / NSubstitute (backend); React 19 / TypeScript / Vitest / Testing Library (frontend); existing `ILlmClient` + channel background workers + SSE.

**Spec:** `docs/superpowers/specs/2026-07-31-conversation-auto-title-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- No C# primary constructors; do not inline object creation into method/ctor calls.
- Windows: no `.ps1`/`.sh`; commits via `git commit -F` message file; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- UI copy / response language: English.
- Placeholder title string exactly: `New conversation`.
- Title model config key: `Areas:Agents:Llm:TitleModel`, default `gpt-4.1-nano`.
- Cadence: suggest after first completed turn, then every 3 completed turns after last successful suggestion.
- Completed turn = one successful `ConversationLoop.ExecuteReplyAsync` (user message already stored; one agent assistant reply finished).

## File Structure

**Backend — create**
- `backend/src/Areas/AgentForge.Areas.Agents/Domain/TitleMode.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationTitleService.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Queue/ConversationTitleQueue.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Queue/ConversationTitleWorker.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Persistence/Migrations/<timestamp>_ConversationAutoTitle.cs` (+ Designer if using `dotnet ef`)
- `backend/tests/AgentForge.Areas.Agents.Unit/ConversationTitleTests.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/ConversationTitleServiceTests.cs`

**Backend — modify**
- `backend/src/Areas/AgentForge.Areas.Agents/Domain/Conversation.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Persistence/EntityConfigurations.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Persistence/Migrations/AgentsDbContextModelSnapshot.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs`
- `backend/src/AgentForge.Host/appsettings.json`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationService.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Requests.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/ConversationEndpoints.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Events/RunEvent.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Queue/ConversationReplyWorker.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/ConversationTests.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs`

**Frontend — create**
- `frontend/src/areas/agents/ConversationTitleHeader.tsx`
- `frontend/src/__tests__/ConversationTitleHeader.test.tsx`

**Frontend — modify**
- `frontend/src/areas/agents/types.ts`
- `frontend/src/areas/agents/api.ts`
- `frontend/src/lib/sse.ts`
- `frontend/src/areas/agents/ConversationPages.tsx`
- `frontend/src/__tests__/agentsApi.test.ts` (if create/title helpers covered)
- `frontend/src/__tests__/sse.test.ts`

---

### Task 1: Domain — TitleMode and Conversation title APIs

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Domain/TitleMode.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Domain/Conversation.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Unit` call sites in `ConversationTests.cs` (pass `TitleMode.Locked` into `Create`)
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/ConversationTitleTests.cs`

**Interfaces:**
- Consumes: existing `Conversation.Create` / `Update` / `AppendMessage`
- Produces:
  - `enum TitleMode { Auto, Paused, Locked }`
  - `Conversation.DefaultAutoTitle` = `"New conversation"`
  - `Create(ownerId, title, titleMode, participantAgentIds, now)` — sets `TitleMode`, `CompletedTurnCount = 0`, `TitleGeneratedAtTurn = null`
  - `bool ShouldSuggestTitle()`
  - `void RecordCompletedTurn(DateTimeOffset now)` — increments `CompletedTurnCount`, sets `UpdatedAt`
  - `bool ApplySuggestedTitle(string title, DateTimeOffset now)` — no-op/`false` unless `Auto`; sets title, `TitleGeneratedAtTurn = CompletedTurnCount`, bumps `ConcurrencyToken`
  - `void SetTitle(string title, Guid concurrencyToken, DateTimeOffset now)` — concurrency check; Auto→Paused; Locked/Paused stay; sets title; bumps token
  - `void LockTitle(Guid concurrencyToken, DateTimeOffset now)` → Locked
  - `void ResumeAutoTitle(Guid concurrencyToken, DateTimeOffset now)` → Auto
  - `Update(...)` — if previous mode was Auto and title string changes, set mode Paused (then apply participants as today)

- [ ] **Step 1: Write failing domain tests**

Create `ConversationTitleTests.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationTitleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-31T12:00:00Z");

    private static Conversation NewAuto()
    {
        var ids = new[] { Guid.CreateVersion7() };
        return Conversation.Create("owner-1", Conversation.DefaultAutoTitle, TitleMode.Auto, ids, Now);
    }

    [Fact]
    public void Create_WhenAuto_SetsModeAndZeroTurns()
    {
        var conversation = NewAuto();
        Assert.Equal(TitleMode.Auto, conversation.TitleMode);
        Assert.Equal(0, conversation.CompletedTurnCount);
        Assert.Null(conversation.TitleGeneratedAtTurn);
        Assert.Equal(Conversation.DefaultAutoTitle, conversation.Title);
    }

    [Fact]
    public void ShouldSuggestTitle_AfterFirstTurn_IsTrue()
    {
        var conversation = NewAuto();
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ShouldSuggestTitle());
    }

    [Fact]
    public void ShouldSuggestTitle_AtTurns2And3_IsFalse_ThenTrueAt4()
    {
        var conversation = NewAuto();
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ApplySuggestedTitle("First", Now));
        conversation.RecordCompletedTurn(Now);
        Assert.False(conversation.ShouldSuggestTitle());
        conversation.RecordCompletedTurn(Now);
        Assert.False(conversation.ShouldSuggestTitle());
        conversation.RecordCompletedTurn(Now);
        Assert.True(conversation.ShouldSuggestTitle());
    }

    [Fact]
    public void SetTitle_WhenAuto_Pauses()
    {
        var conversation = NewAuto();
        var token = conversation.ConcurrencyToken;
        conversation.SetTitle("Manual", token, Now);
        Assert.Equal("Manual", conversation.Title);
        Assert.Equal(TitleMode.Paused, conversation.TitleMode);
    }

    [Fact]
    public void LockTitle_WhenAuto_Locks()
    {
        var conversation = NewAuto();
        conversation.LockTitle(conversation.ConcurrencyToken, Now);
        Assert.Equal(TitleMode.Locked, conversation.TitleMode);
    }

    [Fact]
    public void ResumeAutoTitle_FromPaused_SetsAuto()
    {
        var conversation = NewAuto();
        conversation.SetTitle("Manual", conversation.ConcurrencyToken, Now);
        conversation.ResumeAutoTitle(conversation.ConcurrencyToken, Now);
        Assert.Equal(TitleMode.Auto, conversation.TitleMode);
    }

    [Fact]
    public void ApplySuggestedTitle_WhenPaused_ReturnsFalse()
    {
        var conversation = NewAuto();
        conversation.SetTitle("Manual", conversation.ConcurrencyToken, Now);
        Assert.False(conversation.ApplySuggestedTitle("Ignored", Now));
        Assert.Equal("Manual", conversation.Title);
    }

    [Fact]
    public void SetTitle_WhenLocked_StaysLocked()
    {
        var ids = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create("owner-1", "Named", TitleMode.Locked, ids, Now);
        conversation.SetTitle("Renamed", conversation.ConcurrencyToken, Now);
        Assert.Equal("Renamed", conversation.Title);
        Assert.Equal(TitleMode.Locked, conversation.TitleMode);
    }
}
```

- [ ] **Step 2: Run tests — expect fail**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter ConversationTitleTests`

Expected: FAIL (missing types / methods).

- [ ] **Step 3: Implement domain**

`TitleMode.cs`:

```csharp
namespace AgentForge.Areas.Agents.Domain;

public enum TitleMode
{
    Auto = 0,
    Paused = 1,
    Locked = 2
}
```

Update `Conversation.cs`:
- Add `public const string DefaultAutoTitle = "New conversation";`
- Properties: `TitleMode`, `CompletedTurnCount`, `TitleGeneratedAtTurn` (`int?`)
- Extend `Create` with `TitleMode titleMode` parameter; initialize new fields
- Implement methods listed in Interfaces
- `ShouldSuggestTitle`: `TitleMode == Auto` and (`TitleGeneratedAtTurn is null && CompletedTurnCount >= 1` OR `CompletedTurnCount - TitleGeneratedAtTurn.Value >= 3`)
- Update all existing `Conversation.Create(...)` call sites in unit tests to pass `TitleMode.Locked` (or Auto where intentional)

- [ ] **Step 4: Run tests — expect pass**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~ConversationTitleTests|FullyQualifiedName~ConversationTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```cmd
git add backend/src/Areas/AgentForge.Areas.Agents/Domain/TitleMode.cs backend/src/Areas/AgentForge.Areas.Agents/Domain/Conversation.cs backend/tests/AgentForge.Areas.Agents.Unit/ConversationTitleTests.cs backend/tests/AgentForge.Areas.Agents.Unit/ConversationTests.cs
git commit -F commitmsg.txt
```

Message: `feat: add conversation title mode and suggest cadence on domain`

---

### Task 2: Persistence + TitleModel config

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Persistence/EntityConfigurations.cs`
- Create: migration under `Persistence/Migrations/`
- Modify: `AgentsDbContextModelSnapshot.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs`
- Modify: `backend/src/AgentForge.Host/appsettings.json`

**Interfaces:**
- Consumes: `TitleMode`, new Conversation properties
- Produces: columns on `agents_conversation`; `AgentsLlmOptions.TitleModel` default `"gpt-4.1-nano"`

- [ ] **Step 1: Map properties in `ConversationConfiguration`**

```csharp
builder.Property(conversation => conversation.TitleMode)
    .HasConversion<string>()
    .HasMaxLength(20)
    .IsRequired();
builder.Property(conversation => conversation.CompletedTurnCount).IsRequired();
builder.Property(conversation => conversation.TitleGeneratedAtTurn);
```

- [ ] **Step 2: Add migration**

Prefer:

```cmd
dotnet ef migrations add ConversationAutoTitle --project backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj --startup-project backend/src/AgentForge.Host/AgentForge.Host.csproj --context AgentsDbContext --output-dir Persistence/Migrations
```

If tooling fails, hand-write `Up`/`Down`:
- Add `TitleMode` TEXT NOT NULL default `'Locked'`
- Add `CompletedTurnCount` INTEGER NOT NULL default `0`
- Add `TitleGeneratedAtTurn` INTEGER NULL
- Update snapshot to match

For existing rows: default Locked is correct (named conversations stay locked).

- [ ] **Step 3: Config**

In `AgentsLlmOptions`:

```csharp
public string TitleModel { get; set; } = "gpt-4.1-nano";
```

In `appsettings.json` under `Areas:Agents:Llm` add `"TitleModel": "gpt-4.1-nano"`.

- [ ] **Step 4: Build**

Run: `dotnet build backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

Message: `feat: persist conversation title mode and title model config`

---

### Task 3: ConversationService create + title actions

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationService.cs`
- Modify: `backend/tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/ConversationTitleServiceTests.cs` (optional if methods live on ConversationService — prefer methods on ConversationService in this task)

**Interfaces:**
- Consumes: domain title APIs, `AgentsDbContext`, clock, current user
- Produces:
  - `CreateAsync`: blank title → `DefaultAutoTitle` + `TitleMode.Auto`; non-blank → trim + `Locked`
  - `Task<Result<Conversation>> SetTitleAsync(Guid id, string title, Guid concurrencyToken, CancellationToken ct)`
  - `Task<Result<Conversation>> LockTitleAsync(Guid id, Guid concurrencyToken, CancellationToken ct)`
  - `Task<Result<Conversation>> ResumeAutoTitleAsync(Guid id, Guid concurrencyToken, CancellationToken ct)` — after resume, if `ShouldSuggestTitle()`, enqueue title job (queue wired in Task 5; for this task call an optional `IConversationTitleQueue?` or leave a TODO hook — **prefer inject queue interface stub now**: `IConversationTitleQueue.TryEnqueue(ConversationTitleJob)` no-op implementation until Task 5, OR enqueue only after Task 5 and in this task only test mode transitions without enqueue)
  - `UpdateAsync`: use `conversation.Update`; ensure Auto+title change pauses via domain

**Recommendation:** Implement Set/Lock/Resume on `ConversationService` now without enqueue; Task 5 adds enqueue on Resume + after turns.

- [ ] **Step 1: Rewrite failing create test**

Replace `CreateAsync_WhenAgentsExist_CreatesWithDefaultTitleFromNames` with:

```csharp
[Fact]
public async Task CreateAsync_WhenTitleOmitted_UsesPlaceholderAndAutoMode()
{
    // arrange agents as before
    var result = await conversations.CreateAsync(null, ids, TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
    Assert.Equal(Conversation.DefaultAutoTitle, result.Value!.Title);
    Assert.Equal(TitleMode.Auto, result.Value.TitleMode);
}

[Fact]
public async Task CreateAsync_WhenTitleProvided_LocksTitle()
{
    var result = await conversations.CreateAsync(" My Chat ", ids, TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
    Assert.Equal("My Chat", result.Value!.Title);
    Assert.Equal(TitleMode.Locked, result.Value.TitleMode);
}

[Fact]
public async Task SetTitleAsync_WhenAuto_Pauses()
{
    var created = await conversations.CreateAsync(null, ids, TestContext.Current.CancellationToken);
    var result = await conversations.SetTitleAsync(
        created.Value!.Id,
        "Manual",
        created.Value.ConcurrencyToken,
        TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
    Assert.Equal(TitleMode.Paused, result.Value!.TitleMode);
    Assert.Equal("Manual", result.Value.Title);
}

[Fact]
public async Task LockTitleAsync_WhenAuto_Locks()
{
    var created = await conversations.CreateAsync(null, ids, TestContext.Current.CancellationToken);
    var result = await conversations.LockTitleAsync(
        created.Value!.Id,
        created.Value.ConcurrencyToken,
        TestContext.Current.CancellationToken);
    Assert.Equal(TitleMode.Locked, result.Value!.TitleMode);
}
```

- [ ] **Step 2: Run — expect fail**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter ConversationServiceTests`

- [ ] **Step 3: Implement service methods**

Create path:

```csharp
string resolvedTitle;
TitleMode titleMode;
if (string.IsNullOrWhiteSpace(title))
{
    resolvedTitle = Conversation.DefaultAutoTitle;
    titleMode = TitleMode.Auto;
}
else
{
    resolvedTitle = title.Trim();
    titleMode = TitleMode.Locked;
}

var conversation = Conversation.Create(
    _currentUser.OwnerId,
    resolvedTitle,
    titleMode,
    agentIds,
    _clock.UtcNow);
```

Set/Lock/Resume: load by id+owner, reject archived, call domain, `SaveChangesAsync`, return conversation. Map concurrency mismatch / not found like `UpdateAsync`.

Builder flow already passes `"New agent"` → remains Locked (no code change beyond Create signature).

- [ ] **Step 4: Run — expect pass**

- [ ] **Step 5: Commit**

Message: `feat: create conversations with auto title mode and title actions`

---

### Task 4: HTTP — titleMode on responses + PATCH title

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Requests.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/ConversationEndpoints.cs`
- Create/extend unit tests for response mapping if helpful (`ConversationResponse` From includes TitleMode)

**Interfaces:**
- Consumes: `ConversationService` title methods
- Produces:
  - `ConversationResponse` gains `TitleMode TitleMode` (JSON camelCase `titleMode`; enum as string `"Auto"`/`"Paused"`/`"Locked"` — configure serializer already used by Host; if enum numbers appear, add `JsonStringEnumConverter` or map to lowercase strings in response). **Use lowercase strings in API:** prefer `string TitleMode` on response via `conversation.TitleMode.ToString().ToLowerInvariant()` OR enum + converter. Spec: `"auto" | "paused" | "locked"`. Map explicitly in `From` methods.
  - `PATCH /conversations/{id}/title` body:

```csharp
public sealed record PatchConversationTitleRequest(
    [property: Required] string Action,
    [property: StringLength(200, MinimumLength = 1)] string? Title,
    Guid ConcurrencyToken);
```

Actions: `"set"` (requires Title), `"lock"`, `"resume"`.

- [ ] **Step 1: Extend `ConversationResponse` and all `From` overloads** with `string TitleMode` from entity (`ToLowerInvariant()`).

- [ ] **Step 2: Add endpoint**

In `ConversationEndpoints.Map`:

```csharp
group.MapPatch("/{id:guid}/title", async (
    Guid id,
    PatchConversationTitleRequest request,
    ConversationService conversations,
    CancellationToken ct) =>
{
    Result<Conversation> result;
    if (string.Equals(request.Action, "set", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.ValidationProblem(...);
        }

        result = await conversations.SetTitleAsync(id, request.Title, request.ConcurrencyToken, ct);
    }
    else if (string.Equals(request.Action, "lock", StringComparison.OrdinalIgnoreCase))
    {
        result = await conversations.LockTitleAsync(id, request.ConcurrencyToken, ct);
    }
    else if (string.Equals(request.Action, "resume", StringComparison.OrdinalIgnoreCase))
    {
        result = await conversations.ResumeAutoTitleAsync(id, request.ConcurrencyToken, ct);
    }
    else
    {
        return Results.ValidationProblem(...);
    }

    return result.ToHttpResult(conversation => /* map participants like PUT */);
});
```

Follow existing PUT mapping pattern for participants in the same file.

- [ ] **Step 3: Build + run unit tests**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj`

- [ ] **Step 4: Commit**

Message: `feat: expose titleMode and PATCH conversation title API`

---

### Task 5: Title job queue, worker, SSE, reply-worker hook

**Files:**
- Create: `ConversationTitleQueue.cs`, `ConversationTitleWorker.cs`, `ConversationTitleService.cs` (LLM suggest + apply)
- Modify: `RunEvent.cs` — add `Title`
- Modify: `ConversationReplyWorker.cs`
- Modify: `ConversationService.ResumeAutoTitleAsync` — enqueue when `ShouldSuggestTitle()`
- Modify: `AgentsArea.cs` — register singleton queue, hosted worker, scoped title service
- Create: `ConversationTitleServiceTests.cs`

**Interfaces:**
- Consumes: `ILlmClient`, `AgentsOptions`, `AgentsDbContext`, `IConversationEventBus`, domain
- Produces:
  - `ConversationTitleJob(Guid ConversationId)`
  - `IConversationTitleQueue` with `bool TryEnqueue(ConversationTitleJob job)` — returns false if already queued/running for that id; clear in-flight in `finally` after process
  - `ConversationTitleService.SuggestAndApplyAsync(Guid conversationId, CancellationToken ct)`
  - SSE: `RunEventType.Title` with JSON `{"title":"...","titleMode":"auto","concurrencyToken":"..."}`

- [ ] **Step 1: Failing unit test for title service apply path**

```csharp
[Fact]
public async Task SuggestAndApplyAsync_WhenAuto_UpdatesTitleAndPublishesEvent()
{
    // Arrange: Auto conversation with CompletedTurnCount=1 in DB
    // ScriptedLlmClient with one completion content "Auth bug"
    // Act
    // Assert title, TitleGeneratedAtTurn==1, event bus received Title
}
```

Use in-memory `AgentsDatabase` pattern from `ConversationServiceTests`; NSubstitute or a simple recording `IConversationEventBus`.

- [ ] **Step 2: Implement queue with dedupe**

```csharp
public sealed class ChannelConversationTitleQueue : IConversationTitleQueue
{
    private readonly Channel<ConversationTitleJob> _channel =
        Channel.CreateUnbounded<ConversationTitleJob>();
    private readonly ConcurrentDictionary<Guid, byte> _inflight = new();

    public bool TryEnqueue(ConversationTitleJob job)
    {
        if (!_inflight.TryAdd(job.ConversationId, 0))
        {
            return false;
        }

        if (!_channel.Writer.TryWrite(job))
        {
            _inflight.TryRemove(job.ConversationId, out _);
            return false;
        }

        return true;
    }

    public void MarkCompleted(Guid conversationId) =>
        _inflight.TryRemove(conversationId, out _);

    public IAsyncEnumerable<ConversationTitleJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
```

- [ ] **Step 3: Implement `ConversationTitleService`**

Steps:
1. Load conversation (include messages as needed); abort if archived or not Auto
2. Build `LlmCompletionRequest` with `options.Llm.TitleModel`, low max tokens (~32), temperature ~0.3, system: `Reply with only a short conversation title. No quotes.`
3. User content: last ~12 non-system messages as `role: content` lines
4. Normalize: trim, strip surrounding `"`, clamp 200; if empty, return
5. Reload/re-check Auto; `ApplySuggestedTitle`; SaveChanges
6. Publish `ConversationEvent` with `RunEventType.Title` and serialized payload

Do not throw on LLM failure — log and return.

- [ ] **Step 4: Implement `ConversationTitleWorker`** (mirror reply worker: scope → service → MarkCompleted in finally)

- [ ] **Step 5: Hook `ConversationReplyWorker`**

After each successful `ExecuteReplyAsync`:
1. Resolve `AgentsDbContext` (or a small `IConversationTitleCoordinator`) from scope
2. Load conversation by `job.ConversationId`
3. `RecordCompletedTurn(clock.UtcNow)`; SaveChanges
4. If `ShouldSuggestTitle()`, `titleQueue.TryEnqueue(new ConversationTitleJob(job.ConversationId))`

Then existing `Done` publish in `finally`.

Also in `ResumeAutoTitleAsync`: after save, if `ShouldSuggestTitle()`, TryEnqueue.

- [ ] **Step 6: Register in `AgentsArea`**

```csharp
services.AddSingleton<IConversationTitleQueue, ChannelConversationTitleQueue>();
services.AddHostedService<ConversationTitleWorker>();
services.AddScoped<ConversationTitleService>();
```

Inject `IConversationTitleQueue` into `ConversationService` and `ConversationReplyWorker`.

- [ ] **Step 7: Tests pass + commit**

Message: `feat: background conversation title suggestions over SSE`

---

### Task 6: Frontend — types, API, SSE `title`, title header component

**Files:**
- Modify: `frontend/src/areas/agents/types.ts`
- Modify: `frontend/src/areas/agents/api.ts`
- Modify: `frontend/src/lib/sse.ts`
- Modify: `frontend/src/__tests__/sse.test.ts`
- Create: `frontend/src/areas/agents/ConversationTitleHeader.tsx`
- Create: `frontend/src/__tests__/ConversationTitleHeader.test.tsx`

**Interfaces:**
- Consumes: PATCH API, SSE title payload
- Produces:
  - `TitleMode = 'auto' | 'paused' | 'locked'`
  - `ConversationDto.titleMode: TitleMode`
  - `patchConversationTitle(id, { action, title?, concurrencyToken })`
  - `openEventSource` listens for `'title'`
  - `ConversationTitleHeader` props: `title`, `titleMode`, `concurrencyToken`, `onUpdated(next: { title, titleMode, concurrencyToken })`

- [ ] **Step 1: Failing header tests**

```tsx
it('shows OK when auto and calls lock', async () => {
  const onUpdated = vi.fn()
  // mock patchConversationTitle
  render(
    <ConversationTitleHeader
      conversationId="c1"
      title="New conversation"
      titleMode="auto"
      concurrencyToken="t1"
      onUpdated={onUpdated}
    />,
  )
  await user.click(screen.getByRole('button', { name: 'OK' }))
  expect(patchMock).toHaveBeenCalledWith('c1', { action: 'lock', concurrencyToken: 't1' })
})

it('shows Auto when locked', () => {
  render(<ConversationTitleHeader ... titleMode="locked" />)
  expect(screen.getByRole('button', { name: 'Auto' })).toBeInTheDocument()
})
```

- [ ] **Step 2: Implement API + types + sse `'title'`**

```ts
export type TitleMode = 'auto' | 'paused' | 'locked'

export async function patchConversationTitle(
  id: string,
  body: { action: 'set' | 'lock' | 'resume'; title?: string; concurrencyToken: string },
): Promise<ConversationDto> {
  // PATCH /api/agents/conversations/${id}/title
}
```

In `sse.ts`: `const types = ['status', 'message', 'usage', 'error', 'done', 'title']`

- [ ] **Step 3: Implement `ConversationTitleHeader`**

Behavior:
- Controlled local draft while input focused / dirty; otherwise show `title` prop
- Blur or Enter with changed text → `action: 'set'` → `onUpdated`
- Button: Auto mode → label `OK` → lock; else label `Auto` → resume
- While dirty editing, ignore prop title updates from parent (parent must not overwrite; header can take `externalTitle` only when `!editing`)

- [ ] **Step 4: Vitest pass**

Run: `cd frontend && npm test -- --run ConversationTitleHeader sse`

- [ ] **Step 5: Commit**

Message: `feat: conversation title header and title API client`

---

### Task 7: Wire conversation detail — live title + SSE reconnect

**Files:**
- Modify: `frontend/src/areas/agents/ConversationPages.tsx`

**Interfaces:**
- Consumes: `ConversationTitleHeader`, `openEventSource`, `getConversation`, title SSE
- Produces: detail page with editable title; reconnect after `done`; apply title events

- [ ] **Step 1: Replace `<h1>{conversation.title}</h1>` with `ConversationTitleHeader`**

Wire `onUpdated` to `setConversation` merge of title/titleMode/concurrencyToken; `rememberItem` label update.

- [ ] **Step 2: SSE handling**

Refactor the stream `useEffect` so that:
1. On mount, open EventSource
2. On event `title`, if payload has title/titleMode/concurrencyToken, update conversation state (and recent-item label) **unless** a ref `titleEditingRef` is true (set by header via callback `onEditingChange`)
3. On event `done`, close and **immediately reopen** EventSource (so title worker events after Done are received). Keep message reload behavior as today (`needsMessageReload`)
4. On `done` (or after message reload), optionally `getConversation(id)` and merge title fields as fallback

Pseudo:

```ts
useEffect(() => {
  if (!id) return
  let stopped = false
  let stopStream: (() => void) | undefined

  const connect = () => {
    stopStream = openEventSource(`/api/agents/conversations/${id}/stream`, {
      onEvent: (type, data) => {
        if (type === 'title' && data && typeof data === 'object') {
          // merge into conversation state when !titleEditingRef.current
          return
        }
        dispatch({ type: 'sse', event: type, data })
        if (type === 'done' && !stopped) {
          stopStream?.()
          connect()
        }
      },
      onError: () => undefined,
    })
  }

  void Promise.all([getConversation(id), getConversationMessages(id)]).then(...)
  connect() // or connect after hydrate

  return () => {
    stopped = true
    stopStream?.()
  }
}, [id])
```

Avoid infinite reconnect loops on hard errors: only reconnect after a clean `done` event, not on every `onError`.

- [ ] **Step 3: Manual smoke checklist (no automated E2E required)**

- Create conversation without title → title shows `New conversation`, button OK
- Send addressed message → after reply, title may change via SSE
- Click OK → Locked, button Auto
- Edit title while Auto → Paused
- Click Auto → resumes

- [ ] **Step 4: Run frontend tests + backend unit tests**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj
cd frontend && npm test -- --run
```

- [ ] **Step 5: Commit**

Message: `feat: live conversation titles with lock and auto resume`

---

## Self-review (plan vs spec)

| Spec requirement | Task |
|---|---|
| Title modes Auto/Paused/Locked | 1 |
| Create blank → placeholder + Auto; titled → Locked | 3 |
| Cadence first turn then every 3 | 1, 5 |
| Manual edit pauses; OK locks; Auto resumes | 1, 3, 4, 6 |
| Background title job + dedupe | 5 |
| Fixed TitleModel config | 2, 5 |
| SSE Title events | 5, 6, 7 |
| PATCH title API + titleMode on DTO | 4, 6 |
| Drop participant-name default title | 3 |
| Builder "New agent" stays locked | 3 (title supplied) |
| Fail soft on LLM errors | 5 |
| Frontend header OK/Auto | 6, 7 |
| List updates | 7 (open detail + rememberItem; list refresh on navigate) |

**SSE timing:** Reply worker publishes `Done` before title LLM finishes; Task 7 reconnects after `Done` so Title events still deliver; `getConversation` fallback covered.

**No placeholders left** in task steps; types/names consistent (`TitleMode`, `ShouldSuggestTitle`, `TryEnqueue`, `patchConversationTitle`, `ConversationTitleHeader`).
