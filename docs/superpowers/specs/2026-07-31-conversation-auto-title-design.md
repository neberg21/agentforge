# AgentForge — Conversation auto-title

**Date:** 2026-07-31  
**Status:** Design approved for planning  
**Scope:** Editable conversation titles; background LLM suggestions when created without a title; lock/pause/resume until the user confirms. Backend domain + title job + SSE + API; frontend header controls and list updates.

## Goal

1. Every conversation has a **name** the user can change.
2. If created **without** a title, generate and refresh the title in the background via a fixed cheap LLM model.
3. While suggestions are active, the title updates live until the user clicks **OK** (locks) — unless they pause via manual edit and later resume with **Auto**.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Approach | Background title job + SSE (same pattern as conversation reply jobs) |
| First suggest | After the **first completed turn** (user message + assistant reply) |
| Refresh cadence | Every **3 completed turns** after the last successful suggestion |
| Turn definition | One completed turn = one user message + its assistant reply finished |
| Create with title | Mode **Locked** from the start — no auto-titling |
| Create without title | Placeholder `"New conversation"`, mode **Auto** |
| Manual edit while Auto | Saves title and switches to **Paused** |
| Resume | **Auto** control resumes suggestions (from Paused or Locked) |
| Confirm | **OK** (visible in Auto) → **Locked** |
| Model | Fixed cheap model via config `Areas:Agents:Llm:TitleModel` (default `gpt-4.1-nano`) |
| Old blank-title default | Drop “default to participant agent names”; use placeholder + Auto instead |

## Out of scope

- Builder `"New agent"` flow title changes (stays fixed / locked with its existing title)
- Streaming token-by-token title generation from the LLM
- Auto-titling when a title was supplied at create
- Settings UI for model / cadence (config only)
- Renaming via a separate “name” field (keep single `Title`)

## Domain

### Title mode

Persisted on `Conversation`:

| Mode | Meaning |
|---|---|
| `Auto` | Suggestions keep running on cadence |
| `Paused` | Manual edit paused suggestions; user can resume |
| `Locked` | Title fixed until user explicitly resumes Auto |

### Bookkeeping fields

| Field | Purpose |
|---|---|
| `TitleMode` | `Auto` \| `Paused` \| `Locked` |
| `CompletedTurnCount` | Increments when a user + assistant reply turn completes |
| `TitleGeneratedAtTurn` | `CompletedTurnCount` at last successful suggestion; null until first success |

### Suggest when

Mode is `Auto` and either:

- `TitleGeneratedAtTurn` is null and `CompletedTurnCount >= 1`, or
- `CompletedTurnCount - TitleGeneratedAtTurn >= 3`

### Create rules

- Title omitted / whitespace → `Title = "New conversation"`, `TitleMode = Auto`
- Title provided → trimmed title (max 200), `TitleMode = Locked`
- No longer invent titles from participant names when blank

### Mode transitions

```
Create(blank)  → Auto
Create(title)  → Locked

Auto  + OK           → Locked
Auto  + set title    → Paused
Paused + Auto        → Auto
Locked + Auto        → Auto
Locked + set title   → Locked (title changes; stays locked)
Paused + set title   → Paused (title changes; stays paused)
```

## Backend pipeline

### Trigger

After a completed turn in the conversation reply path: if suggest-when holds, enqueue `ConversationTitleJob` for that conversation id. Skip enqueue when a title job for the same conversation is already queued or running.

### Worker

1. Load conversation; abort if archived or mode ≠ `Auto`.
2. Call `ILlmClient.CompleteAsync` with `TitleModel`, short system prompt (“return only a concise conversation title”), and a truncated recent message window.
3. Normalize output: trim, strip wrapping quotes, clamp to 200 chars; reject empty → keep current title.
4. Re-check mode is still `Auto`; if not, no-op.
5. Persist new `Title`, set `TitleGeneratedAtTurn = CompletedTurnCount`, bump `UpdatedAt` / concurrency as elsewhere.
6. Publish SSE title event.

### Failures

LLM errors / timeouts: log warning, keep title and mode, do not enqueue infinite retries on the same turn (next cadence opportunity may try again).

### Config

```json
"Areas": {
  "Agents": {
    "Llm": {
      "TitleModel": "gpt-4.1-nano"
    }
  }
}
```

## API

### DTO extension

Conversation responses include:

- `title` (existing)
- `titleMode`: `"auto" | "paused" | "locked"`

### Title control endpoint

`PATCH /api/agents/conversations/{id}/title`

Body (discriminated or explicit action):

- `{ "action": "set", "title": "..." }` — apply mode rules above
- `{ "action": "lock" }` — → Locked
- `{ "action": "resume" }` — → Auto; if suggest-when already holds, enqueue a title job

Require concurrency token consistent with other conversation updates.

Existing `PUT /conversations/{id}`: if title changes while mode is `Auto`, transition to `Paused` (same as set).

### SSE

On conversation stream, new event type e.g. `Title`:

```json
{ "title": "...", "titleMode": "auto" }
```

Clients update header and list row when received.

## Frontend

### Conversation detail header

Inline-editable title + one mode button:

| Mode | Button | Click |
|---|---|---|
| Auto | **OK** | lock |
| Paused | **Auto** | resume |
| Locked | **Auto** | resume |

- Editing/saving the title while Auto → PATCH set → Paused
- Subscribe to conversation SSE; apply `Title` events to the field unless the user is mid-edit (don’t clobber an open draft)

### List

Show current `title`; when stream is connected for an open conversation, update that row; otherwise refresh on navigation/reload.

### Create form

Unchanged UX: optional title. Blank → Auto + placeholder; filled → Locked.

## Testing

- Unit: create blank → Auto + placeholder; create with title → Locked
- Unit: cadence at completed turns 1, 4, 7; no suggest at 2, 3, 5, 6
- Unit: mode transitions Auto→Paused→Auto→Locked; Locked set-title stays Locked
- Unit: title job skips persist when mode ≠ Auto
- Integration: SSE Title event after successful suggest; PATCH lock / resume / set
- Frontend: OK vs Auto by mode; edit triggers pause; SSE updates title when not editing

## Architecture note

Prefer a small dedicated title-suggest service + job/queue types next to existing reply infrastructure, rather than embedding LLM title calls inside `ConversationLoop`, so reply latency and title suggestion stay independent and mode checks stay testable.
