# AgentForge — Agent definition suggestions

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Unused German first-name suggestions via Bogus for (1) New agent form prefill and (2) Agent Builder session injection. Shared generator; prompt update; hide System messages in transcript. No DB migration.

## Goal

1. **New agent form:** Prefill the name field with a unique DE first name so create is fast.
2. **Agent Builder:** Each builder session gets a unique suggested name injected as a System message; the builder prompt no longer asks for a name and uses that suggestion in drafts unless the user overrides.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Name source | Bogus `new Faker("de").Person.FirstName` (fresh `Faker` per attempt) |
| Uniqueness | Same as create: conflict only with non-archived agents |
| Collision handling | Retry FirstName (cap ~32); then suffix `-2`, `-3`, … |
| Form API | `GET /api/agents/definitions/suggestions` → `{ name }` (extensible) |
| Form prefill | Create only; set name only if field still empty when response arrives |
| Builder injection | On `POST /builder/session`: generate name → seed **System** message on the new conversation |
| Builder interview | Do **not** ask for a name; use suggestion unless user volunteers another |
| Builder prompt sync | On session start, if Agent Builder exists, **update** its system prompt to current `AgentBuilderDefaults` |
| Transcript | Hide `role === 'System'` messages in conversation UI |
| Session response | Keep `{ conversationId, builderAgentId }` — do **not** add `suggestedName` in v1 |

## Out of scope

- Prefill on edit agent
- Extra suggestion fields beyond `{ name }` for now
- Auto-rename on submit when `agent_name_taken`
- Client-side name generation without Bogus
- Conversation metadata / DB column for suggested name
- Returning `suggestedName` from builder session API in v1

## Shared generation

Add **Bogus** to `AgentForge.Areas.Agents`.

`AgentSuggestionService` (or equivalent):

1. Each attempt: `candidate = new Faker("de").Person.FirstName`.
2. If taken (`NameIsTakenAsync` / shared check) → retry (e.g. 32).
3. If still taken → append `-2`, `-3`, … to last candidate until free.
4. Return the free name.

Used by both the suggestions endpoint and builder session start.

## Backend

### `GET /api/agents/definitions/suggestions`

- Register **before** `GET /definitions/{id:guid}`.
- Response:

```json
{
  "name": "Lena"
}
```

- Always `200` with non-empty `name`.

### `POST /api/agents/builder/session` (extended)

1. Ensure non-archived agent named `Agent Builder`.
2. If missing → create with `AgentBuilderDefaults.Definition`.
3. If present → **update** definition (at least `SystemPrompt`, and preferably other default fields that stay in sync: description/model/tools as already defined) so prompt changes apply without archive/recreate.
4. Generate unused name via shared suggestion helper.
5. Create conversation (title unchanged, e.g. `New agent`).
6. Append a **System** message to that conversation, content along the lines of:

   `Suggested agent name for this session: {name}. Use this exact name in the agent-draft "name" field unless the user explicitly chooses a different name.`

7. Return `{ conversationId, builderAgentId }` (unchanged shape).

Need a conversation API path to append a system message at create time (internal service method; not a public “post system message” endpoint for clients).

`ConversationLoop.BuildMessages` already prepends `agent.SystemPrompt` and then includes history including System roles — no loop change required for injection to reach the model.

### Builder system prompt (`AgentBuilderDefaults.SystemPrompt`)

Rewrite instructions to:

- Interview for purpose/description and system-prompt behavior (essentials); model/tools/limits only if the user asks.
- **Do not ask** what to name the agent.
- Use the session suggested name from the system context for `agent-draft.name`.
- Only change the draft name if the user explicitly provides a different one.
- When proposing: short summary + one `agent-draft` fence (same JSON shape as today).
- Never claim the agent already exists; UI Create persists it.

Exact wording is an implementation detail; keep it in `AgentBuilderDefaults`.

## Frontend

| Module | Responsibility |
|---|---|
| `api.ts` | `getAgentSuggestions()` → `{ name: string }` |
| `AgentFormPage` | Create mount: fetch suggestions; prefill if name still empty |
| Conversation transcript | Do not render messages with `role === 'System'` |

Edit form and builder session client response unchanged otherwise.

## Errors

| Case | Behavior |
|---|---|
| Suggestions GET fails | Leave form name empty |
| Create race (`agent_name_taken`) | Existing form / draft-card handling |
| Builder session fails mid-way after conversation create | Prefer transactional-ish cleanup or fail before exposing id; if system message append fails, return error (do not navigate to a half-ready session) |
| Suggestions under many taken names | Suffix fallback; still 200 |

## Testing

- **Unit — suggestions:** Taken common DE names → result not taken; after archive, that name may appear again.
- **Unit — builder session:** Start seeds a System message containing the suggested name; builder agent prompt matches current defaults after update path.
- **Integration:** `GET .../suggestions` returns name; create with it works; `POST .../builder/session` returns conversation whose messages include a System entry with a name (API may still return it; UI hides it).
- **Frontend:** Create form prefills; transcript does not show System messages; edit does not call suggestions.

## Acceptance

1. Open **New agent** → name shows a DE first name without typing; user can edit it.
2. Create → **archive** that agent → New agent / new builder session can reuse that name.
3. **Edit agent** does not call suggestions or overwrite name.
4. **Create with assistant** → chat has no visible system blob; builder does not ask for a name; draft `name` matches the injected suggestion unless the user overrides in chat.
5. After deploying a prompt change, the next builder session uses the new prompt without manually archiving Agent Builder.

## Extensibility note

`GET .../suggestions` stays an object so later fake fields can be added. Builder session can later expose `suggestedName` if the UI needs it; v1 relies on the System message only.
