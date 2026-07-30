# AgentForge — UI + Conversations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the Agents area with multi-agent conversations (read-only tools, mentions, draft-run, `conversationId` on runs, `q` on definitions) and ship a React SPA for agents, runs, and chat.

**Architecture:** Backend first in `AgentForge.Areas.Agents` (domain → persistence → services → conversation reply worker/loop → HTTP). Then Vite React app at `src/AgentForge.Web/` consuming the full API. Host publishes `dist` to `wwwroot`. No separate “3b” project — everything missing for conversations is in this plan.

**Tech Stack:** .NET 10, EF Core (EnsureCreated), existing LLM/tool/workspace stack; React 19, TypeScript, Vite, Tailwind 4, react-router 7, Vitest.

**Spec:** `docs/superpowers/specs/2026-07-29-agentforge-ui-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- No C# primary constructors; do not inline object creation into method/ctor calls.
- ProblemDetails `code` snake_case; add new codes from the spec (`conversation_not_found`, `mention_not_participant`, `conversation_archived`).
- Conversation tools: **only** `read_file` when workspace enabled; never write/shell; shared `LocalPath` on `BaseRef`, no worktree/push.
- Mentions reply **sequentially** in request order.
- Frontend only under `src/AgentForge.Web/`.
- Windows: no `.ps1`/`.sh`; commits via `git commit -F %TEMP%\commitmsg.txt`; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- xUnit v3, no mocking libraries — hand-written fakes (match existing tests).

## File Structure

**Backend — create**
- `Domain/Conversation.cs`, `ConversationMessage.cs`, `ConversationParticipant.cs` (or owned collection)
- `Domain/AgentErrors.cs` — extend codes
- `Persistence/` configs + `AgentsDbContext` DbSets
- `Application/ConversationService.cs`
- `Application/AgentService.cs` — add `q`
- `Application/RunService.cs` — optional `conversationId`
- `Http/ConversationEndpoints.cs`, request/response records
- `Runtime/Events/IConversationEventBus.cs` (or generalize existing bus by Guid)
- `Runtime/Queue/IConversationReplyQueue.cs`, `ConversationReplyWorker.cs`
- `Runtime/ConversationLoop.cs`
- `Runtime/Workspace/ConversationReadSession.cs` (ensure clone/fetch; bind read root to `LocalPath`)

**Backend — tests**
- `tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs`, `ConversationLoopTests.cs`, …
- `tests/AgentForge.Host.Integration/ConversationEndpointTests.cs`, extend agent/run tests for `q` / `conversationId`

**Frontend — create** (as before under `src/AgentForge.Web/`)
- scaffold, `lib/*`, `shell/*`, `areas/agents/*` including conversation pages

**Host**
- `Program.cs` static + SPA; csproj Publish copy

---

## Part 1 — Backend

### Task 1: Conversation domain + persistence

**Files:**
- Create: `Domain/Conversation.cs`, `Domain/ConversationMessage.cs`
- Modify: `Persistence/AgentsDbContext.cs`, `Persistence/EntityConfigurations.cs`
- Modify: `Domain/AgentErrors.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/ConversationTests.cs`

**Interfaces:**
- `Conversation.Create(ownerId, title, IReadOnlyList<Guid> participantAgentIds, now)` — at least one participant; `ConcurrencyToken`; messages collection
- `Archive(now)`, `Update(title, participantIds, concurrencyToken, now)` → conflict if token mismatch
- `AppendMessage(...)` with optional `senderAgentId`/`senderName`/`mentionsJson`
- Errors: `ConversationNotFound`, `ConversationArchived`, `MentionNotParticipant`

- [ ] **Step 1: Failing domain tests** (create requires participants; archive sets `ArchivedAt`; append increments sequence)

- [ ] **Step 2: Implement entities + EF config** (`agents_conversation`, `agents_conversation_message`, participants as JSON column or join table — prefer JSON array of agent ids on conversation **or** explicit `ConversationParticipant` table with FK; use explicit table if querying by agent later — **use `ConversationParticipant` entity** `(ConversationId, AgentId)` unique)

- [ ] **Step 3: PASS, commit**

```
feat: add conversation domain and persistence
```

### Task 2: ConversationService CRUD + archive

**Files:**
- Create: `Application/ConversationService.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs`

**Interfaces:**
- `CreateAsync(title?, participantAgentIds, ct)` — resolve agents (not archived); default title from names; owner from `ICurrentUser`
- `GetAsync`, `ListAsync(page)` — exclude archived; include participant names (join Agents); last message excerpt/at
- `UpdateAsync(id, title, participantAgentIds, concurrencyToken, ct)`
- `ArchiveAsync(id, ct)` — idempotent like agents
- `GetMessagesAsync(id, ct)`

- [ ] **Step 1: Failing service tests** with in-memory/sqlite test DbContext pattern used by `AgentServiceTests`

- [ ] **Step 2: Implement**

- [ ] **Step 3: PASS, commit**

```
feat: add conversation application service
```

### Task 3: `q` on definitions + `conversationId` on runs

**Files:**
- Modify: `Application/AgentService.cs`, `Http/AgentEndpoints.cs`
- Modify: `Domain/Run.cs`, `Application/RunService.cs`, `Http/Requests.cs`, `Http/Responses.cs`, EF config
- Test: unit + `tests/AgentForge.Host.Integration/AgentEndpointTests.cs`, `RunEndpointTests.cs`

**Interfaces:**
- `ListAsync(PageRequest page, string? q, ct)` — `EF.Functions.Like` / `Contains` case-insensitive on Name when `q` set
- `CreateRunRequest(AgentId, Objective, Guid? ConversationId)`
- `Run.Create(..., Guid? conversationId)`; persist nullable `ConversationId`
- Validate conversation exists, same owner, not archived when provided

- [ ] **Step 1: Failing tests**

- [ ] **Step 2: Implement**

- [ ] **Step 3: PASS, commit**

```
feat: add agent search q and run conversationId
```

### Task 4: Conversation read session + reply loop

**Files:**
- Create: `Runtime/Workspace/IConversationReadSession.cs`, `ConversationReadSession.cs`
- Create: `Runtime/ConversationLoop.cs`
- Create: `Runtime/Events/` conversation bus (reuse `InProcessRunEventBus` pattern keyed by conversation id — e.g. rename conceptually to `IStreamEventBus` **or** duplicate `IConversationEventBus` / `InProcessConversationEventBus` to avoid breaking runs; **prefer duplicate thin copy** for YAGNI rename)
- Create: `Runtime/Queue/IConversationReplyQueue.cs`, `ChannelConversationReplyQueue.cs`, `ConversationReplyWorker.cs`
- Modify: `AgentsArea.cs` DI
- Test: `ConversationLoopTests.cs` with `ScriptedLlmClient` + fake git/read root temp dir

**Interfaces:**
- `ConversationReplyJob(Guid ConversationId, Guid StreamId, IReadOnlyList<Guid> AgentIds)`
- `IConversationReadSession.BeginAsync(ct)` → ensure clone/fetch via `IGitWorkspace`; set `AsyncLocal` root = `LocalPath` (not worktree); `Dispose`/end clears AsyncLocal only (no remove worktree)
- `ConversationLoop.ExecuteReplyAsync(conversationId, agentId, streamId, ct)`:
  - load agent + messages; build LLM request with tools = `[read_file]` iff workspace enabled
  - bind read session; run turn loop like `RunLoop` but append `ConversationMessage` with sender fields; publish on conversation bus
  - never register write/shell for this loop
- Worker: dequeue job; for each agentId sequentially run loop; finally publish `done`

- [ ] **Step 1: Failing loop test** — scripted tool call `read_file` succeeds against temp file under LocalPath; `write_file` not offered in request tools list

- [ ] **Step 2: Implement session, loop, queue, worker, DI**

- [ ] **Step 3: PASS, commit**

```
feat: add conversation reply loop with read-only workspace
```

### Task 5: Post message + SSE + draft-run endpoints

**Files:**
- Create: `Http/ConversationEndpoints.cs`
- Modify: `Http/Requests.cs`, `Responses.cs`, `AgentsArea.MapEndpoints`
- Extend: `ConversationService` with `PostMessageAsync`, `DraftRunAsync`
- Test: `tests/AgentForge.Host.Integration/ConversationEndpointTests.cs`

**Interfaces:**
- `PostMessageAsync(id, content, mentions)`:
  - validate mentions ⊆ participants → else `mention_not_participant`
  - persist user message; if mentions empty → return 202 `{ streamId }` without enqueue (UI may skip stream or open stream and get nothing until later — still return streamId; optional immediate `done` publish)
  - else enqueue job; return 202 `{ streamId }`
- `GET .../stream` — same SSE write pattern as `RunEndpoints.StreamRunAsync` on conversation bus
- `DraftRunAsync(id, agentId?)` — pick agent; LLM completion with system instruction to propose objective from transcript; return `{ objective, agentId }`; no persist run
- Map routes under `/conversations`

- [ ] **Step 1: Integration tests** with Fake LLM — note (no mention) stores one user message; mention yields assistant message; draft returns objective; archived rejects post

- [ ] **Step 2: Implement endpoints + service methods**

- [ ] **Step 3: PASS, commit**

```
feat: add conversation HTTP endpoints stream and draft-run
```

### Task 6: Backend verification

- [ ] **Step 1:** `dotnet test` on `AgentForge.Areas.Agents.Unit` and `AgentForge.Host.Integration` — all green
- [ ] **Step 2:** Commit any fixes `test: harden conversation backend tests`

---

## Part 2 — Frontend

### Task 7: Vite + React + Vitest scaffold

**Files:** scaffold under `src/AgentForge.Web/` (package.json, vite, tsconfig, index.html, main, App, index.css, vitest, test/setup)

**Interfaces:** scripts `dev`/`build`/`test`/`lint`; proxy `/api` to Host URL from `launchSettings.json`

- [ ] Smoke test renders AgentForge; `npm test` + `npm run build` PASS
- [ ] Commit `chore: scaffold AgentForge.Web with Vite React Vitest`

### Task 8: `http.ts` ApiError from `code`

**Files:** `src/lib/http.ts`, `__tests__/http.test.ts`

- [ ] Tests for `agent_name_taken` mapping and omitted empty query keys
- [ ] Implement; commit `feat: add fetch helper with problem-details code mapping`

### Task 9: Shell, area registry, routing

**Files:** `shell/*`, `lib/areas.ts`, `lib/recent.ts`, `areas/index.ts`, stub `areas/agents/routes.tsx`

- [ ] Nav = registry ∩ `/api/areas`; routes for definitions/runs/conversations
- [ ] Commit `feat: add app shell with explicit area registry`

### Task 10: Agents API client + types (incl. conversations)

**Files:** `areas/agents/types.ts`, `api.ts`, `__tests__/agentsApi.test.ts`, `__tests__/conversationsApi.test.ts`

**Interfaces:** DTOs for Agent, Run (+ `conversationId`), RunMessage, Conversation, ConversationMessage, Participant; all list/get/create/update/archive; `postConversationMessage` → 202 `streamId`; `draftRun`; `startRun` with optional `conversationId`; `listAgents` sends `q`

- [ ] Failing URL/body tests; implement; commit `feat: add agents and conversations API client`

### Task 11: Agent list / form / detail

**Files:** `AgentListPage.tsx`, `AgentFormPage.tsx`, `AgentDetailPage.tsx`

- [ ] Debounced `q`, archive, form concurrency/`agent_name_taken`, CTAs for run + conversation
- [ ] Commit `feat: add agent list form and detail pages`

### Task 12: transcriptReducer + ToolCallCard

**Files:** `transcriptReducer.ts`, `ToolCallCard.tsx`, tests

- [ ] Hydrate/dedupe/sse/`needsMessageReload`; commit `feat: add transcript reducer and tool call card`

### Task 13: Runs UI + useRunStream

**Files:** `useRunStream.ts`, `sse.ts`, `fakeEventSource.ts`, `RunListPage.tsx`, `RunDetailPage.tsx`, `StartRunDialog.tsx`, `Transcript.tsx`, `TranscriptLog.tsx`

- [ ] Stream + message reload; cancel; start dialog; commit `feat: add run list detail stream and start dialog`

### Task 14: Conversations UI + mentions + draft run

**Files:** `useConversationStream.ts`, `ConversationListPage.tsx`, `ConversationPage.tsx`, `NewConversationDialog.tsx`, `MessageComposer.tsx`, `DraftRunDialog.tsx`

**Interfaces:**
- `@` mention chips → ids; empty mentions → “Not addressed — no agent will reply”
- Draft run dialog → `draftRun` → edit → `startRun` with `conversationId` → navigate run; run detail links back

- [ ] Tests for mentions POST body, draft handoff, archive
- [ ] Implement; commit `feat: add conversation chat and draft-run UI`

### Task 15: Host static files + Publish

**Files:** `Program.cs`, `AgentForge.Host.csproj`

- [ ] Static files + SPA fallback; Publish runs npm build + copy to wwwroot (not on `dotnet build`)
- [ ] Commit `feat: serve AgentForge.Web from host on publish`

### Task 16: End-to-end verification (manual + automated)

- [ ] `dotnet test` (unit + integration)
- [ ] `cd src\AgentForge.Web` → `npm test` && `npm run lint` && `npm run build`
- [ ] Manual: multi-agent chat with `@`, note without mentions, draft run, linked run, agent CRUD, run cancel
- [ ] Commit fixes if needed

---

## Spec coverage checklist

| Spec item | Task |
|---|---|
| Conversation domain/persistence | 1 |
| Conversation CRUD service | 2 |
| `q` + `conversationId` | 3 |
| Read-only reply loop + worker | 4 |
| HTTP messages/stream/draft-run | 5–6 |
| Web scaffold + http + shell | 7–9 |
| API client | 10 |
| Agent pages | 11 |
| Transcript + runs UI | 12–13 |
| Conversation + draft UI | 14 |
| Host serve | 15 |
| Full verify | 16 |

## Out of scope

- Auth UI, multi-tenant, browser E2E, cost charts, workspace file browser, Blazor, React Query, token-streaming events
