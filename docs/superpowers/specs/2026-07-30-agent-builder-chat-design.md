# AgentForge — Agent builder chat

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Seeded “Agent Builder” agent + list entry to start a 1:1 interview conversation; frontend parses `agent-draft` JSON from assistant messages into an inline confirm card; create uses existing `POST /api/agents/definitions`. One small builder-session endpoint.

## Goal

Help users create new agent definitions through a guided chat:

1. From the **agents list**, start a dedicated builder conversation with one click.
2. A seeded **Agent Builder** agent asks clarifying questions (essentials first; model/tools/limits only if the user asks).
3. When ready, the builder proposes a draft; the UI shows an **inline draft card**.
4. The user confirms **Create agent**; the app persists via the existing create API and **stays in the chat** with a success link to the new agent.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Entry | Button on agents list: **Create with assistant** (alongside New agent) |
| Confirm | Draft in chat → UI confirm → then `POST /definitions` |
| Interview depth | Essentials (name, description, system prompt) + optional deep dive |
| Confirm UI | Inline draft card in the transcript |
| Builder identity | Seeded **real** agent definition named `Agent Builder` |
| After create | Stay in builder chat; success message + link to new agent detail |
| Draft transport | Structured `agent-draft` fenced JSON in the assistant message (Approach 1) |
| Card field edits | Read-only on card in v1; refine via chat |
| Multiple drafts | Each message’s card stays independently creatable until that draft is created |

## Out of scope

- Tool-based agent creation (`create_agent` / `propose_agent_draft`)
- Editable draft-card fields / prefill of `/agents/definitions/new`
- Settings UI for builder name or system prompt
- Persisting “created from message” in the database (v1 uses client session state)
- Changing the manual create form flow

## User flow

1. User opens agents list and clicks **Create with assistant**.
2. Client calls `POST /api/agents/builder/session`.
3. Server ensures non-archived agent named `Agent Builder` for the current user (create with baked-in system prompt if missing), creates a 1:1 conversation (title e.g. `New agent`), returns `{ conversationId, builderAgentId }`.
4. Client navigates to that conversation.
5. Builder interviews; when proposing, includes a human summary plus one `agent-draft` fence.
6. UI strips the fence from visible text and renders **AgentDraftCard**.
7. User clicks **Create agent** → `createAgent` / `POST /definitions` with defaults applied for null/omitted fields.
8. On success: card shows Created + link to `/agents/definitions/:id`; Create disabled for that card. User remains in the conversation.

If the builder was archived or renamed so no non-archived `Agent Builder` exists, the next session call recreates it.

## Draft format

Assistant messages that propose a definition append:

````text
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
````

Rules:

- Parse the **last** `agent-draft` fence in the message body.
- Strip that fence from the visible transcript body.
- `null` or omitted optional fields → same defaults as today’s create form / API.
- Invalid or missing JSON → “Draft incomplete — ask the builder to propose again”; no Create button.
- Required for Create: non-empty `name` and `systemPrompt` after parse (description may be empty/null).

## Backend

### `POST /api/agents/builder/session`

Behavior:

1. Look up non-archived agent for `ICurrentUser` with exact name `Agent Builder`.
2. If none: create via existing `AgentService.CreateAsync` with fixed definition (name, description, system prompt, model/defaults, `allowedTools: []`; builder does not need tools to propose drafts).
3. Create conversation with `participantAgentIds: [builder.Id]` and a fixed/default title.
4. Return `{ conversationId, builderAgentId }`.

No DB migration. No change to `POST /definitions` request shape.

### Builder system prompt (seeded content)

Must instruct the agent to:

- Interview for a new AgentForge agent definition.
- Prefer few questions; cover name, purpose/description, and system-prompt behavior first.
- Only discuss model, temperature, tokens, turns, and tools if the user asks.
- When proposing: short human summary + exactly one `agent-draft` JSON fence with the fields above.
- Never claim the agent already exists; persistence happens only when the user clicks Create.

Exact prompt text is an implementation detail; keep it in one backend constant/resource used by the seed path.

## Frontend

| Module | Responsibility |
|---|---|
| `AgentListPage` | **Create with assistant** → builder session → navigate to conversation |
| `api.ts` | `startBuilderSession()` → `POST .../builder/session` |
| `agentDraft.ts` | Parse/strip/validate `agent-draft`; map to create payload with defaults |
| `AgentDraftCard` | Show fields (read-only), Create, error, Created + link |
| `ConversationPages` (or message renderer) | Detect draft on assistant messages; render card; track created message ids in session state |

Manual **New agent** form and routes remain unchanged.

### Session state for “already created”

Key by conversation message `id` from the API. Refresh may re-enable Create — acceptable for v1.

## Error handling

| Case | Behavior |
|---|---|
| Builder session fails | Error on list; do not navigate |
| Invalid draft | Hint on message; no Create |
| `agent_name_taken` | Show on card; user can ask builder for a new name/draft |
| Other create errors | Show on card; Create remains enabled |
| Network/API errors | Existing toast/error patterns |

## Testing

- **Vitest:** parse/strip last fence; defaults for nulls; invalid fence; no fence; required fields.
- **Backend unit/integration:** session creates builder if missing; reuses existing; returns conversation with that participant; second call creates another conversation with the same builder agent.
- **Optional UI:** card shows Create; after success mock, link appears and Create disables.

## Acceptance

1. Agents list has **Create with assistant** and it opens a 1:1 with Agent Builder.
2. First use seeds Agent Builder; later uses reuse the same non-archived definition.
3. Valid `agent-draft` fence renders an inline card; Create persists via existing definitions API.
4. After create, user stays in the chat with success + link to the new agent; that card cannot create again.
5. Manual New agent form still works as today.
