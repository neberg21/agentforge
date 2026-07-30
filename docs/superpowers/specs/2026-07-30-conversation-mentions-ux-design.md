# AgentForge — Conversation mentions UX

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Frontend mention composer/autocomplete + transcript display; small API DTO extension to expose stored mentions. No DB migration.

## Goal

Make conversation addressing clearer and faster:

1. In **1:1** chats (exactly one participant), every send **always** addresses that agent.
2. Show in the transcript **who** a user message was addressed to.
3. Typing `@` in the composer offers participant autocomplete; mentions come from text only (no chip row).

## Decisions (locked)

| Topic | Choice |
|---|---|
| Approach | UI-only parsing; keep `POST { content, mentions[] }` |
| 1:1 addressing | Always address; on send, if no parsed mention, auto-insert `@Name` then re-parse |
| Auto-insert position | Frontend constant `autoMentionPosition: 'prepend' \| 'append'`, default **`prepend`**; settings UI later |
| Composer chips | Removed; mentions only via `@` text + parse on send |
| `@` menu | Inserts `@Name` (+ trailing space); builds mention list only on send via parse |
| Transcript | Highlight `@Name` in body; show **To:** chips only for mentioned agents **not** already present as `@Name` in text |
| Multi-agent, no `@` | Unchanged: store as note, no reply loop, keep “not addressed” hint |

## Out of scope

- Settings UI for prepend/append
- Server-side `@Name` parsing / rewrite of content
- Contenteditable mention tokens
- Changing reply-loop semantics beyond what mentions already do
- Renaming agents mid-conversation (names matched at send/display against current participants)

## Backend

### Response extension only

`ConversationMessageResponse` / `ConversationMessageDto` gains:

```ts
mentions: string[] | null  // agent IDs; null or [] when none
```

Source: existing `MentionsJson` on `ConversationMessage`. Deserialize JSON Guid array → string IDs in API mapping. No migration.

POST `/api/agents/conversations/{id}/messages` unchanged.

## Frontend

### Composer

- Remove the participant chip toggle row.
- Textarea (or equivalent) with `@` trigger:
  - On `@`, open filtered dropdown of conversation participants (filter by name prefix after `@`).
  - Keyboard: ↑/↓, Enter/Tab insert, Esc close.
  - Insert replaces the active `@query` with `@Name` plus a trailing space.
- On Send:
  1. If participant count === 1 and `parseMentions` is empty → `ensureAutoMention(content, participant, autoMentionPosition)`.
  2. `mentions = parseMentions(content, participants)` (IDs, order = first appearance).
  3. `POST` with final `content` and `mentions`.
  4. Multi-agent + empty mentions → keep existing hint; still POST.

### Parsing rules

- Scan for `@` tokens. A candidate is `@` followed by the **exact** participant name (case-insensitive). Prefer the **longest** matching participant name at that position (so `@leo` wins over `@le` if both were participants).
- Names may contain letters, digits, `_`, `-`, and spaces only if the stored participant name contains spaces (then match the full name string after `@`).
- Unknown `@foo` remains literal text; not added to `mentions`.
- Multiple mentions allowed; duplicate agent IDs collapse to first appearance order.

### Auto-insert formatting

- **prepend:** `@Name` + single space + original content (trimStart of original unchanged except leading space after insert).
- **append:** original content (trimEnd) + single space + `@Name` when original is non-empty; otherwise just `@Name`.
- After insert, re-run `parseMentions` so `mentions` always includes that agent.

### Transcript rendering

For **User** messages with `mentions`:

1. Resolve IDs → participant names (fallback: id short form if participant left).
2. In body text, highlight substrings that are `@` + known participant name (case-insensitive).
3. For each mentioned agent whose `@Name` does **not** appear in the body, render a **To: @Name** chip (header/meta area).
4. If all mentions are already represented as `@Name` in text, show **no** To chips.
5. Assistant/tool/system: no To chips.

### Config constant

```ts
// e.g. frontend/src/areas/agents/mentionConfig.ts
export const autoMentionPosition: 'prepend' | 'append' = 'prepend'
```

## Components / modules (suggested)

| Module | Responsibility |
|---|---|
| `mentionConfig.ts` | `autoMentionPosition` |
| `mentions.ts` | `parseMentions`, `ensureAutoMention`, `mentionsMissingFromText` |
| `MentionTextarea` (or hook) | `@` menu + keyboard |
| Message render helper | Highlight + conditional To chips |
| `ConversationPages` | Wire composer + display; drop chip row |

Keep helpers pure and unit-tested; page stays thin.

## Testing

- **Vitest:** parse (single/multi/unknown/case), ensureAutoMention prepend/append, chip visibility when `@` present vs absent.
- **Vitest (UI light):** `@` opens menu; selecting inserts text (optional if DOM-heavy).
- **Backend unit:** `ConversationMessageResponse.From` / mapping includes `mentions` when `MentionsJson` set.

## Error handling

- Unchanged API errors (`mention_not_participant` only if client sends bad IDs — should not happen if parse is correct).
- Send failures: surface existing API error path (catch in `onSend` if still uncaught).

## Acceptance

1. 1:1 send without typing `@` still stores mention + content with auto-inserted `@Name` (prepend by default) and triggers reply.
2. Typing `@` shows participant assistance; selection inserts `@Name`.
3. Transcript shows highlighted `@` and To chips only when needed per rules above.
4. Multi-agent without `@` remains a note with hint.
5. Message GET returns `mentions` for addressed user messages.
