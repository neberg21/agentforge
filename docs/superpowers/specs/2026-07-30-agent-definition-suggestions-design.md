# AgentForge — Agent definition suggestions

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Prefill the New agent form with an unused German first name via Bogus; one extensible suggestions endpoint. No DB migration.

## Goal

When opening **New agent**, the name field is prefilled with a plausible German first name that is not already taken by a non-archived agent, so the user can create quickly without inventing a unique name.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Entry | Prefill on New agent form only (not edit) |
| Name source | Bogus `new Faker("de").Person.FirstName` |
| API | `GET /api/agents/definitions/suggestions` → `{ name }` (extensible later) |
| Uniqueness | Same as create: conflict only with non-archived agents |
| Collision handling | Retry FirstName (cap); then suffix `-2`, `-3`, … on last candidate |
| Prefill overwrite | Only if name field still empty when response arrives |
| Suggestions failure | Leave name empty; form remains usable |

## Out of scope

- Prefill on edit agent
- Agent Builder draft names
- Extra suggestion fields (`description`, etc.) beyond `{ name }` for now
- Auto-rename on submit when `agent_name_taken`
- Client-side name generation without Bogus

## Backend

### Package

Add **Bogus** to `AgentForge.Areas.Agents`.

### Endpoint

`GET /api/agents/definitions/suggestions`

- Register the static route **before** `GET /definitions/{id:guid}`.
- Response DTO (JSON camelCase as elsewhere):

```json
{
  "name": "Lena"
}
```

- Always `200` with a non-empty `name` (fallback suffixes under heavy collision).
- No query parameters in v1.

### Generation

1. Each attempt: `candidate = new Faker("de").Person.FirstName` (fresh `Faker` so `Person` is not cached across retries).
2. If taken per existing name-taken rules → retry (e.g. up to 32 attempts).
3. If still taken → take last candidate and append `-2`, `-3`, … until free.
4. Return `{ Name: candidate }`.

Reuse the same uniqueness rule as `AgentService` create/update (`NameIsTakenAsync` / equivalent: non-archived only). Prefer a small dedicated helper/service (e.g. `AgentSuggestionService`) that calls into that check rather than duplicating query logic.

## Frontend

| Module | Responsibility |
|---|---|
| `api.ts` | `getAgentSuggestions()` → `{ name: string }` |
| `AgentFormPage` | On create mount only: fetch suggestions; if `form.name` still empty, set name |

Edit flow unchanged (no suggestions call). Existing `agent_name_taken` field error remains for rare races.

## Errors

| Case | Behavior |
|---|---|
| Suggestions GET fails / network | Leave name empty; no blocking error required |
| Create race (`agent_name_taken`) | Existing form field error |
| Suggestions under many taken names | Suffix fallback; still 200 |

## Testing

- **Backend unit:** With several common DE first names already present as non-archived agents, `SuggestAsync` returns a name that is not taken; after archive of an agent, that name may be suggested again.
- **Backend integration:** `GET /api/agents/definitions/suggestions` returns non-empty `name`; `POST /definitions` with that name succeeds.
- **Frontend (light/optional):** Create form mounts and eventually shows a non-empty name when the API succeeds; edit form does not call suggestions.

## Acceptance

1. Open **New agent** → name field shows a German first name without typing.
2. Create with that name → **archive** that agent → open **New agent** again → create succeeds (suggested name may reuse the archived name; archived names do not block).
3. **Edit agent** does not call suggestions or overwrite the existing name.
4. User can clear/change the prefilled name before submit.

## Extensibility note

The path is plural `suggestions` and the body is an object so later fields (e.g. sample description) can be added without a new route. v1 ships `name` only.
