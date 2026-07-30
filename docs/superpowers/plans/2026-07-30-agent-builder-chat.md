# Agent Builder Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users create agent definitions via a guided 1:1 chat with a seeded Agent Builder, confirm an inline draft card, and persist with the existing definitions API.

**Architecture:** `POST /api/agents/builder/session` ensures a real agent named `Agent Builder` and opens a conversation. The builder’s system prompt requires an `agent-draft` fenced JSON block when proposing. The frontend parses/strips that fence, shows a read-only `AgentDraftCard`, and on Create calls `POST /api/agents/definitions`. No tool-based create; no DB migration.

**Tech Stack:** .NET 10 / xUnit v3; React 19, TypeScript, Vite, Vitest.

**Spec:** `docs/superpowers/specs/2026-07-30-agent-builder-chat-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- No C# primary constructors; do not inline object creation into method/ctor calls.
- No DB migration.
- Frontend under `frontend/`.
- Windows: no `.ps1`/`.sh`; commits via message file + `git commit -F`; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- UI copy: English.

## File Structure

**Backend — create**
- `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentBuilderDefaults.cs` — name, model, system prompt, `AgentDefinition`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/BuilderSessionService.cs` — ensure builder + create conversation
- `backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs`

**Backend — modify**
- `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs` — `FindActiveByNameAsync`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs` — `BuilderSessionResponse`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs` — `POST /builder/session`
- `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs` — register `BuilderSessionService`

**Frontend — create**
- `frontend/src/areas/agents/agentDraft.ts` — parse, strip, defaults, create payload
- `frontend/src/areas/agents/AgentDraftCard.tsx` — read-only card + Create
- `frontend/src/__tests__/agentDraft.test.ts`
- `frontend/src/__tests__/AgentDraftCard.test.tsx` (optional light)

**Frontend — modify**
- `frontend/src/areas/agents/api.ts` — `startBuilderSession`
- `frontend/src/areas/agents/types.ts` — `BuilderSessionDto`
- `frontend/src/areas/agents/AgentListPage.tsx` — Create with assistant
- `frontend/src/areas/agents/ConversationPages.tsx` — render draft card; session created map
- `frontend/src/__tests__/agentsApi.test.ts` — session client test

---

### Task 1: Parse `agent-draft` fences (frontend)

**Files:**
- Create: `frontend/src/areas/agents/agentDraft.ts`
- Create: `frontend/src/__tests__/agentDraft.test.ts`

**Interfaces:**
- Consumes: assistant message `content: string`
- Produces:
  - `parseAgentDraft(content: string): AgentDraftParseResult`
  - `stripAgentDraftFence(content: string): string`
  - `toCreateAgentBody(draft: ValidAgentDraft): Record<string, unknown>`
  - Types: `ValidAgentDraft`, `AgentDraftParseResult` (`{ ok: true, draft }` | `{ ok: false, reason: 'missing' | 'invalid' }`)

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/__tests__/agentDraft.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import {
  parseAgentDraft,
  stripAgentDraftFence,
  toCreateAgentBody,
} from '../areas/agents/agentDraft'

const fence = (json: string) =>
  `Here is your agent.\n\n\`\`\`agent-draft\n${json}\n\`\`\`\n`

describe('parseAgentDraft', () => {
  it('parses the last agent-draft fence', () => {
    const content =
      fence('{"name":"Old","systemPrompt":"old"}') +
      '\n' +
      fence(
        JSON.stringify({
          name: 'Coder',
          description: 'Writes code',
          systemPrompt: 'You write code.',
          model: null,
          temperature: null,
          maxOutputTokens: null,
          maxTurns: null,
          allowedTools: null,
        }),
      )
    const result = parseAgentDraft(content)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.draft.name).toBe('Coder')
    expect(result.draft.systemPrompt).toBe('You write code.')
    expect(result.draft.description).toBe('Writes code')
  })

  it('returns missing when no fence', () => {
    expect(parseAgentDraft('just chat').ok).toBe(false)
    if (parseAgentDraft('just chat').ok) return
    expect(parseAgentDraft('just chat').reason).toBe('missing')
  })

  it('returns invalid when JSON is broken or required fields empty', () => {
    expect(parseAgentDraft(fence('{')).reason).toBe('invalid')
    expect(parseAgentDraft(fence('{"name":"","systemPrompt":"x"}')).reason).toBe(
      'invalid',
    )
    expect(parseAgentDraft(fence('{"name":"A","systemPrompt":""}')).reason).toBe(
      'invalid',
    )
  })
})

describe('stripAgentDraftFence', () => {
  it('removes the last agent-draft fence from visible body', () => {
    const content = fence(
      '{"name":"Coder","systemPrompt":"You write code."}',
    )
    const visible = stripAgentDraftFence(content)
    expect(visible).not.toContain('agent-draft')
    expect(visible).toContain('Here is your agent.')
  })
})

describe('toCreateAgentBody', () => {
  it('fills defaults for null optional fields', () => {
    const body = toCreateAgentBody({
      name: 'Coder',
      description: null,
      systemPrompt: 'You write code.',
      model: null,
      temperature: null,
      maxOutputTokens: null,
      maxTurns: null,
      allowedTools: null,
    })
    expect(body).toEqual({
      name: 'Coder',
      description: null,
      systemPrompt: 'You write code.',
      model: 'gpt-4.1-mini',
      temperature: 0.7,
      maxOutputTokens: 4096,
      maxTurns: 20,
      allowedTools: [],
    })
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\frontend && npm test -- --run src/__tests__/agentDraft.test.ts"`

Expected: FAIL (module not found / exports missing).

- [ ] **Step 3: Implement `agentDraft.ts`**

Create `frontend/src/areas/agents/agentDraft.ts`:

```ts
const FENCE_RE = /```agent-draft\s*\n([\s\S]*?)```/gi

export const agentDraftDefaults = {
  model: 'gpt-4.1-mini',
  temperature: 0.7,
  maxOutputTokens: 4096,
  maxTurns: 20,
  allowedTools: [] as string[],
}

export type ValidAgentDraft = {
  name: string
  description: string | null
  systemPrompt: string
  model: string | null
  temperature: number | null
  maxOutputTokens: number | null
  maxTurns: number | null
  allowedTools: string[] | null
}

export type AgentDraftParseResult =
  | { ok: true; draft: ValidAgentDraft }
  | { ok: false; reason: 'missing' | 'invalid' }

function lastFenceMatch(content: string): RegExpExecArray | null {
  const re = new RegExp(FENCE_RE.source, FENCE_RE.flags)
  let last: RegExpExecArray | null = null
  let match: RegExpExecArray | null
  while ((match = re.exec(content)) !== null) {
    last = match
  }
  return last
}

export function stripAgentDraftFence(content: string): string {
  const last = lastFenceMatch(content)
  if (!last) {
    return content
  }
  const start = last.index
  const end = start + last[0].length
  return (content.slice(0, start) + content.slice(end)).trimEnd()
}

export function parseAgentDraft(content: string): AgentDraftParseResult {
  const last = lastFenceMatch(content)
  if (!last) {
    return { ok: false, reason: 'missing' }
  }
  try {
    const raw = JSON.parse(last[1]) as Record<string, unknown>
    const name = typeof raw.name === 'string' ? raw.name.trim() : ''
    const systemPrompt =
      typeof raw.systemPrompt === 'string' ? raw.systemPrompt.trim() : ''
    if (!name || !systemPrompt) {
      return { ok: false, reason: 'invalid' }
    }
    const description =
      raw.description === null || raw.description === undefined
        ? null
        : typeof raw.description === 'string'
          ? raw.description
          : null
    const draft: ValidAgentDraft = {
      name,
      description,
      systemPrompt,
      model: typeof raw.model === 'string' ? raw.model : null,
      temperature: typeof raw.temperature === 'number' ? raw.temperature : null,
      maxOutputTokens:
        typeof raw.maxOutputTokens === 'number' ? raw.maxOutputTokens : null,
      maxTurns: typeof raw.maxTurns === 'number' ? raw.maxTurns : null,
      allowedTools: Array.isArray(raw.allowedTools)
        ? raw.allowedTools.map(String)
        : null,
    }
    return { ok: true, draft }
  } catch {
    return { ok: false, reason: 'invalid' }
  }
}

export function toCreateAgentBody(draft: ValidAgentDraft): Record<string, unknown> {
  return {
    name: draft.name,
    description: draft.description,
    systemPrompt: draft.systemPrompt,
    model: draft.model ?? agentDraftDefaults.model,
    temperature: draft.temperature ?? agentDraftDefaults.temperature,
    maxOutputTokens: draft.maxOutputTokens ?? agentDraftDefaults.maxOutputTokens,
    maxTurns: draft.maxTurns ?? agentDraftDefaults.maxTurns,
    allowedTools: draft.allowedTools ?? agentDraftDefaults.allowedTools,
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\frontend && npm test -- --run src/__tests__/agentDraft.test.ts"`

Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
git add frontend/src/areas/agents/agentDraft.ts frontend/src/__tests__/agentDraft.test.ts
git commit -F path\to\msg.txt
```

Message: `test: add agent-draft parse helpers for builder chat`

---

### Task 2: Builder session service (backend)

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentBuilderDefaults.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/BuilderSessionService.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs`

**Interfaces:**
- Consumes: `AgentService.CreateAsync`, `AgentService.FindActiveByNameAsync`, `ConversationService.CreateAsync`
- Produces:
  - `AgentBuilderDefaults.Name` = `"Agent Builder"`
  - `AgentBuilderDefaults.Model` = `"gpt-4.1-mini"` (must match frontend `agentDraftDefaults.model`)
  - `AgentBuilderDefaults.Definition` → `AgentDefinition`
  - `BuilderSessionService.StartAsync(ct)` → `Result<BuilderSession>`
  - `BuilderSession` record: `(Guid ConversationId, Guid BuilderAgentId)`
  - `AgentService.FindActiveByNameAsync(string name, CancellationToken ct)` → `Task<Agent?>`

- [ ] **Step 1: Write the failing tests**

Create `backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs`:

```csharp
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Llm;
using AgentForge.Areas.Agents.Runtime.Queue;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class BuilderSessionServiceTests
{
    private sealed class RecordingReplyQueue : IConversationReplyQueue
    {
        public void Enqueue(ConversationReplyJob job)
        {
        }

        public async IAsyncEnumerable<ConversationReplyJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private static (
        AgentsDbContext Context,
        BuilderSessionService Builder,
        AgentService Agents) NewServices(AgentsDatabase database, IClock clock)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var queue = new RecordingReplyQueue();
        var events = new InProcessConversationEventBus();
        var llm = new ScriptedLlmClient(
            [new LlmCompletionResult("ok", [], new LlmUsage(1, 1))]);
        var conversations = new ConversationService(
            context, database.CurrentUser, clock, queue, events, llm);
        var builder = new BuilderSessionService(agents, conversations);
        return (context, builder, agents);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderMissing_CreatesBuilderAndConversation()
    {
        using var database = new AgentsDatabase();
        var (context, builder, _) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;

        var result = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var agents = await context.Agents.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(agents, a => a.Name == AgentBuilderDefaults.Name && a.ArchivedAt == null);
        Assert.Equal(1, await context.Conversations.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(result.Value!.BuilderAgentId, agents.Single(a => a.Name == AgentBuilderDefaults.Name).Id);
    }

    [Fact]
    public async Task StartAsync_WhenBuilderExists_ReusesSameAgent()
    {
        using var database = new AgentsDatabase();
        var (context, builder, agents) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var first = await builder.StartAsync(TestContext.Current.CancellationToken);
        var second = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.BuilderAgentId, second.Value!.BuilderAgentId);
        Assert.NotEqual(first.Value.ConversationId, second.Value.ConversationId);
        Assert.Equal(1, await context.Agents.CountAsync(
            a => a.Name == AgentBuilderDefaults.Name && a.ArchivedAt == null,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_WhenBuilderArchived_RecreatesBuilder()
    {
        using var database = new AgentsDatabase();
        var (context, builder, agents) = NewServices(database, TestClock.AtEpoch());
        await using var _ = context;
        var first = await builder.StartAsync(TestContext.Current.CancellationToken);
        await agents.ArchiveAsync(first.Value!.BuilderAgentId, TestContext.Current.CancellationToken);

        var second = await builder.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.BuilderAgentId, second.Value!.BuilderAgentId);
        Assert.Equal(2, await context.Agents.CountAsync(
            a => a.Name == AgentBuilderDefaults.Name,
            TestContext.Current.CancellationToken));
    }
}
```

Adjust `RecordingReplyQueue` / LLM helpers to match `ConversationServiceTests` if compile requires more interface members.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\backend && dotnet test tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter BuilderSessionServiceTests"`

Expected: FAIL (types missing).

- [ ] **Step 3: Add `FindActiveByNameAsync` on `AgentService`**

In `AgentService.cs`, add:

```csharp
public Task<Agent?> FindActiveByNameAsync(string name, CancellationToken ct) =>
    _db.Agents.FirstOrDefaultAsync(
        agent => agent.Name == name && agent.ArchivedAt == null && agent.OwnerId == _currentUser.OwnerId,
        ct);
```

(Confirm `OwnerId` filter matches other queries in `AgentService`; if list already scopes by owner via global filter, omit redundant predicate — match existing query style.)

- [ ] **Step 4: Implement defaults + session service**

Create `AgentBuilderDefaults.cs` (no primary ctor):

```csharp
namespace AgentForge.Areas.Agents.Application;

public static class AgentBuilderDefaults
{
    public const string Name = "Agent Builder";
    public const string Model = "gpt-4.1-mini";
    public const string ConversationTitle = "New agent";

    public const string Description =
        "Helps you design a new AgentForge agent through a short interview.";

    public const string SystemPrompt = """
        You are Agent Builder for AgentForge. Your job is to interview the user and propose a new agent definition.

        Ask a few clarifying questions. Cover essentials first: name, purpose/description, and the system-prompt behavior.
        Only discuss model, temperature, max output tokens, max turns, or allowed tools if the user asks to tune them.

        When you are ready to propose, write a short human summary, then append exactly one fenced JSON block with language tag agent-draft:

        ```agent-draft
        {
          "name": "...",
          "description": "...",
          "systemPrompt": "...",
          "model": null,
          "temperature": null,
          "maxOutputTokens": null,
          "maxTurns": null,
          "allowedTools": null
        }
        ```

        Use null for optional fields the user did not specify. Never claim the agent already exists; the user creates it with a Create button in the UI.
        """;

    public static AgentDefinition Definition { get; } = new(
        Name,
        Description,
        SystemPrompt,
        Model,
        Agent.DefaultTemperature,
        Agent.DefaultMaxOutputTokens,
        Agent.DefaultMaxTurns,
        []);
}
```

Add `using AgentForge.Areas.Agents.Domain;` as needed.

Create `BuilderSession.cs` content in the same file as the service or separate:

```csharp
namespace AgentForge.Areas.Agents.Application;

public sealed record BuilderSession(Guid ConversationId, Guid BuilderAgentId);

public sealed class BuilderSessionService
{
    private readonly AgentService _agents;
    private readonly ConversationService _conversations;

    public BuilderSessionService(AgentService agents, ConversationService conversations)
    {
        _agents = agents;
        _conversations = conversations;
    }

    public async Task<Result<BuilderSession>> StartAsync(CancellationToken ct)
    {
        var existing = await _agents.FindActiveByNameAsync(AgentBuilderDefaults.Name, ct);
        Agent builder;
        if (existing is null)
        {
            var created = await _agents.CreateAsync(AgentBuilderDefaults.Definition, ct);
            if (!created.IsSuccess)
            {
                return created.Error!.Value;
            }

            builder = created.Value!;
        }
        else
        {
            builder = existing;
        }

        var participantIds = new[] { builder.Id };
        var conversation = await _conversations.CreateAsync(
            AgentBuilderDefaults.ConversationTitle,
            participantIds,
            ct);
        if (!conversation.IsSuccess)
        {
            return conversation.Error!.Value;
        }

        var session = new BuilderSession(conversation.Value!.Id, builder.Id);
        return session;
    }
}
```

Register in `AgentsArea.ConfigureServices`:

```csharp
services.AddScoped<BuilderSessionService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\backend && dotnet test tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter BuilderSessionServiceTests"`

Expected: PASS.

- [ ] **Step 6: Commit**

Message: `feat: add builder session service for Agent Builder seeding`

---

### Task 3: `POST /api/agents/builder/session` endpoint

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs`
- Optionally extend: `backend/tests/AgentForge.Host.Integration/ApiClient.cs` + one integration test if easy; unit coverage from Task 2 is enough — prefer a thin endpoint test only if the project already maps endpoints in unit tests. Otherwise skip integration and rely on unit + manual smoke.

**Interfaces:**
- Consumes: `BuilderSessionService.StartAsync`
- Produces: `BuilderSessionResponse(Guid ConversationId, Guid BuilderAgentId)` at `POST /api/agents/builder/session` → **201** with body; Location optional ` /api/agents/conversations/{id}`

- [ ] **Step 1: Add response record**

In `Responses.cs`:

```csharp
public sealed record BuilderSessionResponse(Guid ConversationId, Guid BuilderAgentId)
{
    public static BuilderSessionResponse From(BuilderSession session) =>
        new(session.ConversationId, session.BuilderAgentId);
}
```

Add `using AgentForge.Areas.Agents.Application;` if not present.

- [ ] **Step 2: Map endpoint**

In `AgentEndpoints.MapAgentEndpoints`, **after** the `/definitions` group (sibling route under `/api/agents`):

```csharp
routes.MapPost("/builder/session", async (BuilderSessionService service, CancellationToken ct) =>
{
    var started = await service.StartAsync(ct);
    return started.ToHttpResult(session =>
    {
        var response = BuilderSessionResponse.From(session);
        return TypedResults.Created(
            $"/api/agents/conversations/{session.ConversationId}",
            response);
    });
});
```

Do not nest under `/definitions`.

- [ ] **Step 3: Build**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\backend && dotnet build src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj"`

Expected: SUCCESS.

- [ ] **Step 4: Commit**

Message: `feat: expose POST /api/agents/builder/session`

---

### Task 4: List entry + API client

**Files:**
- Modify: `frontend/src/areas/agents/types.ts`
- Modify: `frontend/src/areas/agents/api.ts`
- Modify: `frontend/src/areas/agents/AgentListPage.tsx`
- Modify: `frontend/src/__tests__/agentsApi.test.ts`

**Interfaces:**
- Consumes: `POST /api/agents/builder/session`
- Produces: `startBuilderSession(): Promise<BuilderSessionDto>` where `BuilderSessionDto = { conversationId: string; builderAgentId: string }`

- [ ] **Step 1: Write failing API client test**

Append to `frontend/src/__tests__/agentsApi.test.ts`:

```ts
import { startBuilderSession } from '../areas/agents/api'

it('startBuilderSession posts builder session', async () => {
  const fetchMock = vi.fn(
    async () =>
      new Response(
        JSON.stringify({
          conversationId: 'c1',
          builderAgentId: 'a1',
        }),
        { status: 201, headers: { 'Content-Type': 'application/json' } },
      ),
  )
  vi.stubGlobal('fetch', fetchMock)
  const session = await startBuilderSession()
  expect(session).toEqual({ conversationId: 'c1', builderAgentId: 'a1' })
  const [url, init] = fetchMock.mock.calls[0]!
  expect(String(url)).toBe('/api/agents/builder/session')
  expect(init?.method).toBe('POST')
})
```

- [ ] **Step 2: Run test — expect fail**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\frontend && npm test -- --run src/__tests__/agentsApi.test.ts"`

- [ ] **Step 3: Add type + API**

In `types.ts`:

```ts
export type BuilderSessionDto = {
  conversationId: string
  builderAgentId: string
}
```

In `api.ts`:

```ts
export function startBuilderSession(): Promise<BuilderSessionDto> {
  return apiSend('POST', '/api/agents/builder/session', {})
}
```

(If `apiSend` omits body for empty object, pass `{}` or `undefined` consistently with other POSTs.)

- [ ] **Step 4: Wire AgentListPage**

Replace the single New agent link with a button group:

```tsx
<div className="flex items-center gap-2">
  <button
    type="button"
    className="rounded border border-[var(--border)] px-3 py-1.5 text-sm"
    disabled={startingBuilder}
    onClick={() => {
      setStartingBuilder(true)
      void startBuilderSession()
        .then((session) => {
          navigate(`/agents/conversations/${session.conversationId}`)
        })
        .catch((err: ApiError) => {
          setError(err.detail ?? err.title)
        })
        .finally(() => setStartingBuilder(false))
    }}
  >
    Create with assistant
  </button>
  <Link
    to="/agents/definitions/new"
    className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white"
  >
    New agent
  </Link>
</div>
```

Add `const [startingBuilder, setStartingBuilder] = useState(false)` and import `startBuilderSession`. Confirm conversation detail route is `/agents/conversations/:id` (match existing Chat navigation / `routes.tsx`).

- [ ] **Step 5: Run API tests — expect pass**

- [ ] **Step 6: Commit**

Message: `feat: start Agent Builder session from agents list`

---

### Task 5: Inline draft card in conversation transcript

**Files:**
- Create: `frontend/src/areas/agents/AgentDraftCard.tsx`
- Create: `frontend/src/__tests__/AgentDraftCard.test.tsx`
- Modify: `frontend/src/areas/agents/ConversationPages.tsx`
- Modify: `frontend/src/areas/agents/MessageBody.tsx` only if needed — prefer stripping in the page before `MessageBody` so MessageBody stays mention-focused

**Interfaces:**
- Consumes: `parseAgentDraft`, `stripAgentDraftFence`, `toCreateAgentBody`, `createAgent`, `ApiError`
- Produces: `AgentDraftCard` props:
  - `messageId: string`
  - `draft: ValidAgentDraft`
  - `createdAgentId: string | null`
  - `onCreated: (messageId: string, agentId: string) => void`

- [ ] **Step 1: Write failing card test**

Create `frontend/src/__tests__/AgentDraftCard.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AgentDraftCard } from '../areas/agents/AgentDraftCard'
import type { ValidAgentDraft } from '../areas/agents/agentDraft'

const draft: ValidAgentDraft = {
  name: 'Coder',
  description: 'Writes code',
  systemPrompt: 'You write code.',
  model: null,
  temperature: null,
  maxOutputTokens: null,
  maxTurns: null,
  allowedTools: null,
}

vi.mock('../areas/agents/api', () => ({
  createAgent: vi.fn(async () => ({ id: 'new-1', name: 'Coder' })),
}))

describe('AgentDraftCard', () => {
  it('creates agent and shows link', async () => {
    const onCreated = vi.fn()
    render(
      <MemoryRouter>
        <AgentDraftCard
          messageId="m1"
          draft={draft}
          createdAgentId={null}
          onCreated={onCreated}
        />
      </MemoryRouter>,
    )
    fireEvent.click(screen.getByRole('button', { name: /create agent/i }))
    await waitFor(() => expect(onCreated).toHaveBeenCalledWith('m1', 'new-1'))
    // Re-render as created:
    render(
      <MemoryRouter>
        <AgentDraftCard
          messageId="m1"
          draft={draft}
          createdAgentId="new-1"
          onCreated={onCreated}
        />
      </MemoryRouter>,
    )
    expect(screen.getByRole('link', { name: /open agent/i })).toHaveAttribute(
      'href',
      '/agents/definitions/new-1',
    )
  })
})
```

If Testing Library / MemoryRouter patterns differ in repo, mirror `MentionTextarea.test.tsx`.

- [ ] **Step 2: Implement `AgentDraftCard`**

```tsx
import { useState } from 'react'
import { Link } from 'react-router'
import { createAgent } from './api'
import type { ValidAgentDraft } from './agentDraft'
import { toCreateAgentBody } from './agentDraft'
import type { ApiError } from '../../lib/http'

type Props = {
  messageId: string
  draft: ValidAgentDraft
  createdAgentId: string | null
  onCreated: (messageId: string, agentId: string) => void
}

export function AgentDraftCard({
  messageId,
  draft,
  createdAgentId,
  onCreated,
}: Props) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (createdAgentId) {
    return (
      <div className="mt-2 rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm">
        <div className="font-medium">Created: {draft.name}</div>
        <Link className="underline" to={`/agents/definitions/${createdAgentId}`}>
          Open agent
        </Link>
      </div>
    )
  }

  return (
    <div className="mt-2 rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm">
      <div className="mb-2 font-medium">Proposed agent</div>
      <dl className="space-y-1">
        <div>
          <dt className="text-xs text-[var(--muted)]">Name</dt>
          <dd>{draft.name}</dd>
        </div>
        {draft.description ? (
          <div>
            <dt className="text-xs text-[var(--muted)]">Description</dt>
            <dd>{draft.description}</dd>
          </div>
        ) : null}
        <div>
          <dt className="text-xs text-[var(--muted)]">System prompt</dt>
          <dd className="whitespace-pre-wrap">{draft.systemPrompt}</dd>
        </div>
      </dl>
      {error ? <p className="mt-2 text-sm text-red-600">{error}</p> : null}
      <button
        type="button"
        className="mt-3 rounded bg-[var(--accent)] px-3 py-1.5 text-white"
        disabled={busy}
        onClick={() => {
          setBusy(true)
          setError(null)
          const body = toCreateAgentBody(draft)
          void createAgent(body)
            .then((agent) => onCreated(messageId, agent.id))
            .catch((err: ApiError) => {
              if (err.code === 'agent_name_taken') {
                setError(
                  'That name is already taken. Ask the builder for a new name.',
                )
              } else {
                setError(err.detail ?? err.title ?? 'Create failed')
              }
            })
            .finally(() => setBusy(false))
        }}
      >
        Create agent
      </button>
    </div>
  )
}
```

- [ ] **Step 3: Wire `ConversationPage` transcript**

In the message map inside `ConversationPages.tsx`:

1. Add `const [createdDrafts, setCreatedDrafts] = useState<Record<string, string>>({})`.
2. For each message with `role === 'Assistant'` and non-null content:
   - `const parsed = parseAgentDraft(message.content)`
   - `const visible = stripAgentDraftFence(message.content)` when a fence exists (always strip last fence if present, even if invalid)
   - Pass `visible` (or original if no fence) to `MessageBody`
   - If `parsed.ok`, render:

```tsx
<AgentDraftCard
  messageId={message.id}
  draft={parsed.draft}
  createdAgentId={createdDrafts[message.id] ?? null}
  onCreated={(id, agentId) =>
    setCreatedDrafts((prev) => ({ ...prev, [id]: agentId }))
  }
/>
```

3. If `!parsed.ok && parsed.reason === 'invalid'` and a fence was present, show hint text: `Draft incomplete — ask the builder to propose again.`

Detect fence presence via `parseAgentDraft` reason `invalid` vs `missing`, or export `hasAgentDraftFence` if cleaner.

- [ ] **Step 4: Run frontend tests**

Run: `cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\frontend && npm test -- --run src/__tests__/agentDraft.test.ts src/__tests__/AgentDraftCard.test.tsx src/__tests__/agentsApi.test.ts"`

Expected: PASS.

- [ ] **Step 5: Commit**

Message: `feat: render agent draft cards and create from conversation`

---

### Task 6: Smoke verification

**Files:** none (manual / automated suite)

- [ ] **Step 1: Run backend unit tests for builder**

`cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\backend && dotnet test tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter BuilderSessionServiceTests"`

Expected: PASS.

- [ ] **Step 2: Run frontend related tests**

`cmd /c "cd /d C:\Users\NEWA002\source\repos\agentforge\frontend && npm test -- --run src/__tests__/agentDraft.test.ts src/__tests__/AgentDraftCard.test.tsx src/__tests__/agentsApi.test.ts"`

Expected: PASS.

- [ ] **Step 3: Manual acceptance checklist**

1. Agents list shows **Create with assistant** next to **New agent**.
2. First click seeds Agent Builder and opens a 1:1 conversation titled “New agent”.
3. Second click reuses the same builder agent, new conversation.
4. When an assistant message contains a valid `agent-draft` fence, transcript hides JSON and shows the card; Create persists the agent; stay in chat with Open agent link; Create disabled for that card.
5. Manual New agent form still works.

No commit required unless smoke finds fixes — then commit those fixes separately.

---

## Spec coverage self-check

| Spec requirement | Task |
|---|---|
| List button Create with assistant | 4 |
| Seeded Agent Builder / ensure session | 2, 3 |
| Interview prompt (essentials + optional deep dive + fence) | 2 (`AgentBuilderDefaults.SystemPrompt`) |
| Parse/strip `agent-draft` | 1 |
| Inline read-only draft card + Create | 5 |
| `POST /definitions` on confirm | 5 |
| Stay in chat + link | 5 |
| Session state for created message ids | 5 |
| Name taken / invalid draft errors | 5 |
| Vitest parse tests | 1 |
| Backend session unit tests | 2 |
| Manual form unchanged | 4 (additive only) |
| Out of scope (tools, editable card, DB created-from) | not planned |

## Placeholder / consistency self-check

- Model default `gpt-4.1-mini` aligned in `AgentBuilderDefaults.Model` and `agentDraftDefaults.model`.
- `BuilderSession` / `BuilderSessionResponse` / `BuilderSessionDto` field names: `ConversationId`/`conversationId`, `BuilderAgentId`/`builderAgentId`.
- Conversation route must match existing app routes when wiring navigate in Task 4.
- `ConversationService` ctor is `(db, currentUser, clock, replyQueue, events, llm)` — no `AgentService` parameter.
- Conversation navigate path: `/agents/conversations/${session.conversationId}`.
