# AgentForge — UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a React SPA over the AgentForge API for agent CRUD, run start/watch (SSE + messages), and — once Teilprojekt 3b exists — multi-agent conversations with draft-run handoff.

**Architecture:** Vite+React app at `src/AgentForge.Web/` (not in the .NET solution). Explicit area registry mirrors `AddArea<>()`. Shared `http`/`sse` helpers; agents area owns pages, API client, transcript reducer/hooks. Host publishes `dist` into `wwwroot` with SPA fallback. Phase A uses only existing APIs; Phase B is blocked until conversations + draft-run + `q` + optional `conversationId` land in 3b.

**Tech Stack:** React 19, TypeScript, Vite, Tailwind 4, react-router 7, Vitest, Testing Library; ASP.NET Core Host for static serve.

**Spec:** `docs/superpowers/specs/2026-07-29-agentforge-ui-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`. Commands from there unless noted.
- Frontend lives only under `src/AgentForge.Web/`. Do not add a .NET Web project or put frontend under `tests/`.
- Error discrimination: ProblemDetails extension field `code` (snake_case). Never match on message text or `type` path segments.
- Known codes: `agent_not_found`, `run_not_found`, `agent_name_taken`, `concurrency_conflict`, `agent_archived`, `run_invalid_transition`.
- Run SSE events today: `status` | `message` | `usage` | `error` | `done`. No `token`, no dedicated `tool` event. Tools come from `RunMessage.toolCallsJson` / role `Tool`.
- JSON from API is camelCase. TypeScript DTOs use camelCase properties matching System.Text.Json defaults.
- **Windows:** no `.ps1` / `.sh`. Use `cmd /c` or direct `dotnet` / `npm` / `git`. Commits: write message to `%TEMP%\commitmsg.txt`, then `git commit -F %TEMP%\commitmsg.txt`.
- English commit subjects: `feat:` / `test:` / `chore:` / `docs:`.
- After each task: commit only that task’s files.
- **Stop gate:** Before any Phase B task, verify conversations API exists (`GET /api/agents/conversations` returns non-404). If not, stop and report: Phase A done, Phase B blocked on 3b.

## File Structure

**Create — scaffolding**
- `src/AgentForge.Web/package.json`
- `src/AgentForge.Web/vite.config.ts`
- `src/AgentForge.Web/tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`
- `src/AgentForge.Web/index.html`
- `src/AgentForge.Web/src/main.tsx`, `App.tsx`, `index.css`
- `src/AgentForge.Web/src/test/setup.ts`, `fakeEventSource.ts`
- `src/AgentForge.Web/vitest.config.ts`

**Create — lib + shell**
- `src/AgentForge.Web/src/lib/http.ts` — fetch wrapper → `ApiError` with `code`
- `src/AgentForge.Web/src/lib/sse.ts` — EventSource helper
- `src/AgentForge.Web/src/lib/areas.ts` — load `/api/areas`
- `src/AgentForge.Web/src/lib/recent.ts` — localStorage recent items
- `src/AgentForge.Web/src/shell/AppShell.tsx`, `AreaNav.tsx`, `ContextPanel.tsx`
- `src/AgentForge.Web/src/areas/index.ts` — explicit registry

**Create — agents area**
- `src/AgentForge.Web/src/areas/agents/types.ts`
- `src/AgentForge.Web/src/areas/agents/api.ts`
- `src/AgentForge.Web/src/areas/agents/routes.tsx`
- `src/AgentForge.Web/src/areas/agents/transcriptReducer.ts`
- `src/AgentForge.Web/src/areas/agents/useRunStream.ts`
- `src/AgentForge.Web/src/areas/agents/Transcript.tsx`, `TranscriptLog.tsx`, `ToolCallCard.tsx`, `MessageComposer.tsx`
- Pages: `AgentListPage.tsx`, `AgentFormPage.tsx`, `AgentDetailPage.tsx`, `RunListPage.tsx`, `RunDetailPage.tsx`, `StartRunDialog.tsx`
- Phase B: `ConversationListPage.tsx`, `ConversationPage.tsx`, `NewConversationDialog.tsx`, `DraftRunDialog.tsx`, `useConversationStream.ts`

**Modify — Host**
- `src/AgentForge.Host/AgentForge.Host.csproj` — Publish target copy `Web/dist` → `wwwroot`
- `src/AgentForge.Host/Program.cs` — static files + SPA fallback (after MapAreas)

**Tests**
- `src/AgentForge.Web/src/__tests__/*.test.ts(x)` colocated under Web

---

## Phase A — buildable against current API

### Task 1: Vite + React + Vitest scaffold

**Files:**
- Create: all scaffolding files under `src/AgentForge.Web/` listed above (package, vite, tsconfig, index.html, main, App stub, index.css, vitest setup)
- Test: `src/AgentForge.Web/src/__tests__/smoke.test.tsx`

**Interfaces:**
- Produces: `npm run dev` / `build` / `test` / `lint` scripts; Vite proxies `/api` to `http://localhost:5xxx` (match Host launch URL from `launchSettings.json` — read Host and set proxy target accordingly, default `http://localhost:5080` if unclear)

- [ ] **Step 1: Read Host launch URL**

Run: `cmd /c "type src\AgentForge.Host\Properties\launchSettings.json"`
Note `applicationUrl` for the Vite proxy `target`.

- [ ] **Step 2: Scaffold package and configs**

`package.json` dependencies (pin current stable majors matching spec): `react`, `react-dom`, `react-router`; dev: `vite`, `@vitejs/plugin-react`, `typescript`, `tailwindcss` `@tailwindcss/vite`, `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`, `eslint` + typescript-eslint flat config as needed.

`vite.config.ts`:

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
      // adjust port from launchSettings
    },
  },
})
```

Proxy must not buffer SSE (Vite default for proxy is fine if `http-proxy` streams; do not add response buffering middleware).

- [ ] **Step 3: Smoke test**

```tsx
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from '../App'

describe('smoke', () => {
  it('renders app shell placeholder', () => {
    render(<App />)
    expect(screen.getByText(/AgentForge/i)).toBeInTheDocument()
  })
})
```

`App.tsx` temporarily renders `<h1>AgentForge</h1>` until Task 3.

- [ ] **Step 4: Run tests and build**

```
cd src\AgentForge.Web
npm install
npm test
npm run build
```

Expected: PASS; `dist/` created.

- [ ] **Step 5: Commit**

```
chore: scaffold AgentForge.Web with Vite React Vitest
```

---

### Task 2: `http.ts` — ProblemDetails → ApiError

**Files:**
- Create: `src/AgentForge.Web/src/lib/http.ts`
- Test: `src/AgentForge.Web/src/__tests__/http.test.ts`

**Interfaces:**
- Produces:
  - `export type ApiError = { status: number; code: string; title: string; detail: string | null; fieldErrors: Record<string, string[]> }`
  - `export async function apiGet<T>(path: string, query?: Record<string, string | number | undefined>): Promise<T>`
  - `export async function apiSend<T>(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<T>`
  - Empty 204/empty body → `null as T` only when caller expects void; prefer `Promise<void>` overload or `apiSendVoid`.
  - `code` from JSON property `code`; if missing → `'unknown'`.
  - Query: omit `undefined` and empty-string keys.

- [ ] **Step 1: Failing tests**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet, ApiError } from '../lib/http'

afterEach(() => vi.unstubAllGlobals())

describe('apiGet', () => {
  it('maps problem details code extension', async () => {
    vi.stubGlobal('fetch', vi.fn(async () =>
      new Response(JSON.stringify({
        title: 'Conflict',
        detail: 'taken',
        code: 'agent_name_taken',
      }), { status: 409, headers: { 'Content-Type': 'application/problem+json' } }),
    ))
    await expect(apiGet('/api/agents/definitions')).rejects.toMatchObject({
      status: 409,
      code: 'agent_name_taken',
    })
  })

  it('omits empty query values', async () => {
    const fetchMock = vi.fn(async () =>
      new Response(JSON.stringify({ items: [], total: 0, skip: 0, take: 50 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await apiGet('/api/agents/definitions', { q: '', skip: 0, take: 50 })
    const url = String(fetchMock.mock.calls[0]![0])
    expect(url).toBe('/api/agents/definitions?skip=0&take=50')
  })
})
```

- [ ] **Step 2: Run — expect FAIL**

```
cd src\AgentForge.Web && npm test -- http
```

- [ ] **Step 3: Implement `http.ts`**

Throw plain object or `class ApiError extends Error` with the fields above; tests use `toMatchObject`. Parse `errors` object for validation field errors when present (ASP.NET validation problem shape).

- [ ] **Step 4: Tests PASS, commit**

```
feat: add fetch helper with problem-details code mapping
```

---

### Task 3: Shell, area registry, routing

**Files:**
- Create: `shell/AppShell.tsx`, `AreaNav.tsx`, `ContextPanel.tsx`, `lib/areas.ts`, `lib/recent.ts`, `areas/index.ts`, `areas/agents/routes.tsx` (stub routes redirecting to placeholders)
- Modify: `App.tsx`, `main.tsx`
- Test: `src/__tests__/shell.test.tsx`

**Interfaces:**
- Produces:
  - `export type AreaModule = { slug: string; title: string; routes: RouteObject[]; nav: { to: string; label: string }[] }`
  - `export const areaRegistry: AreaModule[]` — starts with agents stub
  - `loadAreas(): Promise<{ slug: string; title: string }[]>` from `GET /api/areas`
  - Nav shows intersection of registry ∩ `/api/areas`
  - Routes: `/` → first area; `/agents` → `/agents/definitions`; nested under `/agents/*`
  - Context panel: React context `setContext(node: ReactNode)` filled by pages

- [ ] **Step 1: Failing test — nav intersection**

Stub `fetch` for `/api/areas` returning `[{ slug: 'agents', title: 'Agents' }]`. Render app with router memory history. Expect link “Agents” / definitions nav. If registry has only agents and API returns empty, expect no area links.

- [ ] **Step 2: Implement shell + registry + stub agents routes**

Placeholder pages: `<p>Agents</p>`, `<p>Runs</p>` for `/agents/definitions` and `/agents/runs`.

- [ ] **Step 3: Tests PASS, commit**

```
feat: add app shell with explicit area registry
```

---

### Task 4: Agents API client + types

**Files:**
- Create: `areas/agents/types.ts`, `areas/agents/api.ts`
- Test: `src/__tests__/agentsApi.test.ts`

**Interfaces:**
- Produces types matching API camelCase:

```ts
export type AgentDto = {
  id: string
  name: string
  description: string | null
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type AgentSnapshotDto = {
  name: string
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
}

export type RunDto = {
  id: string
  agentId: string
  agentSnapshot: AgentSnapshotDto
  objective: string
  status: string
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  error: string | null
  promptTokens: number | null
  completionTokens: number | null
  costEstimate: number | null
  concurrencyToken: string
  conversationId?: string | null // optional until 3b; ignore if absent
}

export type RunMessageDto = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  createdAt: string
}

export type Paged<T> = { items: T[]; total: number; skip: number; take: number }
```

- API functions: `listAgents({ q?, skip, take })`, `getAgent`, `createAgent`, `updateAgent`, `archiveAgent`, `listRuns({ agentId?, status?, skip, take })`, `getRun`, `startRun({ agentId, objective, conversationId? })`, `cancelRun(id, concurrencyToken)`, `getRunMessages(id)`.
- Until `q` exists server-side: still send `q` when non-empty; if server ignores it, client may additionally filter current page by name (document in comment). Prefer server `q` once 3b/host adds it.

- [ ] **Step 1: Tests** — assert URLs/bodies for `listAgents`, `startRun`, `cancelRun` with stubbed `fetch`.

- [ ] **Step 2: Implement, PASS, commit**

```
feat: add agents area API client and DTOs
```

---

### Task 5: Agent list / form / detail pages

**Files:**
- Create: `AgentListPage.tsx`, `AgentFormPage.tsx`, `AgentDetailPage.tsx`
- Wire in `routes.tsx`
- Test: `src/__tests__/agentPages.test.tsx`

**Interfaces:**
- Consumes: `api.ts`, shell context, `rememberItem` from `recent.ts`
- List: debounced `q` 300ms, page size 50, archive confirm dialog, actions start run / start conversation (conversation action can navigate to `/agents/conversations/new` query `agentId=` even if Phase B page is stub — or hide until Phase B; **hide conversation CTAs until Phase B** to avoid dead ends).
- Form: sections Identity, System prompt, Model & limits, Tools (chip input). Client validation matches server ranges. On `agent_name_taken` / `concurrency_conflict` show field/banner per spec.
- Detail: show prompt; buttons edit, archive, start run.

- [ ] **Step 1: Tests** with stubbed API — list renders name; archive calls DELETE; form submit POST; 409 `concurrency_conflict` shows reload affordance.

- [ ] **Step 2: Implement pages (< ~200 lines each; extract small helpers if needed).**

- [ ] **Step 3: PASS, commit**

```
feat: add agent list form and detail pages
```

---

### Task 6: transcriptReducer + ToolCallCard

**Files:**
- Create: `transcriptReducer.ts`, `ToolCallCard.tsx`, optionally shared `Transcript.tsx` skeleton
- Test: `src/__tests__/transcriptReducer.test.ts`

**Interfaces:**
- Produces:

```ts
export type TranscriptMessage = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  senderAgentId?: string | null
  senderName?: string | null
  pending?: boolean
}

export type TranscriptState = {
  bySequence: Record<number, TranscriptMessage>
  status: string | null
  usage: { promptTokens?: number; completionTokens?: number; costEstimate?: number } | null
  error: string | null
  done: boolean
}

export type TranscriptAction =
  | { type: 'hydrate'; messages: TranscriptMessage[] }
  | { type: 'sse'; event: string; data: unknown }
  | { type: 'reloadMessages'; messages: TranscriptMessage[] }

export function transcriptReducer(state: TranscriptState, action: TranscriptAction): TranscriptState
export function messagesInOrder(state: TranscriptState): TranscriptMessage[]
```

- On `sse` `message`: if payload lacks full content, set flag so hook re-fetches messages (reducer can set `needsMessageReload: true` on state — add that field).
- Deduplicate by `sequence` (last write wins).
- Parse `toolCallsJson` in `ToolCallCard` for display (name + args summary); collapsed by default.

- [ ] **Step 1: Unit tests** — hydrate; duplicate sequence; `status`/`usage`/`error`/`done` events; `needsMessageReload` on sparse message event.

- [ ] **Step 2: Implement, PASS, commit**

```
feat: add transcript reducer and tool call card
```

---

### Task 7: useRunStream + Run detail / list / start dialog

**Files:**
- Create: `useRunStream.ts`, `sse.ts` (if not done), `RunListPage.tsx`, `RunDetailPage.tsx`, `StartRunDialog.tsx`, `Transcript.tsx`, `TranscriptLog.tsx`
- Test: `src/__tests__/runStream.test.tsx`, `src/__tests__/runPages.test.tsx`
- Create: `src/test/fakeEventSource.ts`

**Interfaces:**
- `openEventSource(url: string, handlers: { onEvent(type: string, data: unknown): void; onError(): void }): () => void` cleanup
- `useRunStream(runId: string)`: load `getRun` + `getRunMessages`, open `/api/agents/runs/{id}/stream`, dispatch into reducer; when `needsMessageReload`, call `getRunMessages` again and `reloadMessages`.
- Run detail: chat vs log toggle; cancel when Pending/Running with token; map `run_invalid_transition` / `concurrency_conflict`; sticky scroll.
- Start dialog: objective + agent; `agent_archived` message; navigate to run detail on success.
- Fake EventSource for tests: queue events, support `close`.

- [ ] **Step 1: Reducer/stream tests with fake EventSource** — status Running → message → reload path → done.

- [ ] **Step 2: Page tests** — list filter; detail cancel; start dialog.

- [ ] **Step 3: Implement, PASS, commit**

```
feat: add run list detail stream and start dialog
```

---

### Task 8: Host static files + SPA fallback + Publish copy

**Files:**
- Modify: `src/AgentForge.Host/Program.cs`
- Modify: `src/AgentForge.Host/AgentForge.Host.csproj`
- Optional: `wwwroot/.gitkeep` not required if publish creates it

**Interfaces:**
- After `MapAreas()`, in non-dev-or-always: `UseDefaultFiles` + `UseStaticFiles` for `wwwroot`.
- Fallback: for non-file, non-`/api`, non-`/_health`, non-openapi routes → `index.html` (use `MapFallbackToFile("index.html")` carefully so it does not steal `/api`).
- csproj Target `PublishFrontend` BeforeTargets `Publish` or AfterTargets `ComputeFilesToPublish`: run `npm ci` + `npm run build` in `../AgentForge.Web` only on Publish (not on `dotnet build`), copy `dist/**` to `wwwroot`.

Example target sketch:

```xml
<Target Name="PublishFrontend" BeforeTargets="PrepareForPublish" Condition="'$(SkipFrontendPublish)' != 'true'">
  <Exec WorkingDirectory="$(MSBuildThisFileDirectory)..\AgentForge.Web" Command="npm ci" />
  <Exec WorkingDirectory="$(MSBuildThisFileDirectory)..\AgentForge.Web" Command="npm run build" />
  <ItemGroup>
    <DistFiles Include="$(MSBuildThisFileDirectory)..\AgentForge.Web\dist\**\*.*" />
  </ItemGroup>
  <Copy SourceFiles="@(DistFiles)" DestinationFolder="$(MSBuildThisFileDirectory)wwwroot\%(RecursiveDir)" />
</Target>
```

- [ ] **Step 1: Implement Program + csproj.**

- [ ] **Step 2: Manual check** — `npm run build` in Web, copy once manually if needed, `dotnet run --project src/AgentForge.Host`, open `/agents/definitions`, hard-reload `/agents/runs/{some-id}` → not API 404 HTML.

- [ ] **Step 3: Commit**

```
feat: serve AgentForge.Web from host on publish
```

---

### Task 9: Phase A verification

- [ ] **Step 1: Run**

```
cd src\AgentForge.Web
npm test
npm run lint
npm run build
```

Expected: clean.

- [ ] **Step 2: Against running Host** — create agent, start run, open run detail, observe status/messages (Fake LLM or configured NanoGPT). Cancel path once.

- [ ] **Step 3: Commit any fixes**

```
test: finish phase A UI verification fixes
```

**Phase A done.** Agent + Run UI satisfies spec criteria 1–3 and 7–9 (partially: error tests for codes exercised in unit tests; conversation criteria deferred).

---

## Phase B — blocked on Teilprojekt 3b

**Gate:** `GET /api/agents/conversations` must not be 404. Also require: `q` on definitions, optional `conversationId` on create run + on `RunDto`, `POST .../draft-run`.

If gate fails: **do not implement Phase B**; report blocked.

### Task 10: Conversation types + API + stream hook

**Files:**
- Modify: `src/AgentForge.Web/src/areas/agents/types.ts`, `api.ts`
- Create: `src/AgentForge.Web/src/areas/agents/useConversationStream.ts`
- Test: `src/AgentForge.Web/src/__tests__/conversationsApi.test.ts`

**Interfaces:**
- Types:

```ts
export type ParticipantDto = { agentId: string; name: string }

export type ConversationDto = {
  id: string
  title: string
  participants: ParticipantDto[]
  lastMessageExcerpt: string | null
  lastMessageAt: string | null
  createdAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type ConversationMessageDto = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  createdAt: string
  senderAgentId: string | null
  senderName: string | null
}
```

- Functions: `listConversations`, `getConversation`, `createConversation({ title?, participantAgentIds })`, `updateConversation`, `archiveConversation`, `getConversationMessages`, `postConversationMessage(id, { content, mentions })` expecting **202** body `{ streamId: string }`, `draftRun(id, body?: { agentId?: string })` → `{ objective: string; agentId: string }`
- `startRun` body includes `conversationId` when defined
- `useConversationStream(conversationId)`: `getConversationMessages` then `EventSource` on `/api/agents/conversations/{id}/stream`; map messages into `TranscriptMessage` (set `senderAgentId`/`senderName`); reuse `transcriptReducer`

- [ ] **Step 1: Failing tests** — `postConversationMessage` uses POST and reads `streamId` from 202; `draftRun` POSTs to `.../draft-run`; `startRun` serializes `conversationId`.

- [ ] **Step 2: Implement types/api/hook**

- [ ] **Step 3: PASS, commit**

```
feat: add conversation API client and stream hook
```

### Task 11: Conversation list + page + composer mentions

**Files:**
- Create: `ConversationListPage.tsx`, `ConversationPage.tsx`, `NewConversationDialog.tsx`, `MessageComposer.tsx`
- Modify: `routes.tsx`, agent pages (show “Gespräch beginnen”)
- Test: `src/__tests__/conversationPages.test.tsx`

**Interfaces:**
- Composer: typing `@` opens filtered participant list; selecting adds chip; submit sends `mentions: string[]` (agent ids). Empty mentions allowed.
- UI copy when empty mentions: message saved, show “nicht adressiert” (or English UI string if the app is English — match surrounding UI language; prefer English UI strings in code: “Not addressed — no agent will reply”).
- `senderColor(agentId: string): string` — stable HSL from hash of id
- List archive with confirm; new conversation dialog multi-select agents

- [ ] **Step 1: Tests** — mention chip → POST body; empty mentions still POSTs and shows not-addressed hint; list archive calls DELETE.

- [ ] **Step 2: Implement pages**

- [ ] **Step 3: PASS, commit**

```
feat: add conversation list and chat pages
```

### Task 12: DraftRunDialog

**Files:**
- Create: `DraftRunDialog.tsx`
- Modify: `ConversationPage.tsx`, `RunDetailPage.tsx` (link when `conversationId`)
- Test: `src/__tests__/draftRun.test.tsx`

**Interfaces:**
- Button “Draft run” → `draftRun(conversationId)` → dialog fields `objective`, `agentId` (select among participants) prefilled → confirm → `startRun({ agentId, objective, conversationId })` → navigate `/agents/runs/:id`
- Run detail context: if `run.conversationId`, link to `/agents/conversations/:id`

- [ ] **Step 1: Test** — draft then start asserts `conversationId` in startRun body and navigation.

- [ ] **Step 2: Implement**

- [ ] **Step 3: PASS, commit**

```
feat: add draft-run handoff from conversations
```

### Task 13: Phase B verification

- [ ] **Step 1:** `cd src\AgentForge.Web` → `npm test` && `npm run lint` && `npm run build` — clean
- [ ] **Step 2:** Manual against Host+3b — three participants, `@` one agent, note without mentions, draft run, open linked run and back-link
- [ ] **Step 3:** Commit any fixes with `test:` / `fix:` as appropriate

---

## Spec coverage checklist

| Spec item | Task |
|---|---|
| Scaffold Web + Tailwind + Vitest | 1 |
| `code`-based ApiError | 2 |
| Shell / registry / `/api/areas` ∩ nav | 3 |
| Agent CRUD UI | 4–5 |
| Run list/detail/SSE/tools from messages | 6–7 |
| Host wwwroot + SPA + Publish | 8 |
| Phase A verify | 9 |
| Conversations + mentions + read-only (API) | 10–11 (3b) |
| Draft run + `conversationId` | 12 (3b) |
| Error table tests | 2, 5, 7, 11 |
| Criteria 4–6 | Phase B |

## Out of scope (do not implement)

- Auth UI, multi-tenant, E2E, cost charts, workspace file browser, Blazor, React Query, token-streaming UI until API emits `token`
