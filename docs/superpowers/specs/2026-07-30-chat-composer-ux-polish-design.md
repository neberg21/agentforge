# AgentForge — Chat composer UX polish

**Date:** 2026-07-30  
**Status:** Design approved for planning  
**Scope:** Frontend only — conversation composer keyboard shortcuts, message-log sticky auto-scroll, global button cursor.

## Goal

Polish everyday chat interaction:

1. **Enter** sends; **Shift+Enter** inserts a newline.
2. When a new message arrives, auto-scroll to the end **only if** the user is already near the bottom.
3. All buttons show a pointer (click-hand) cursor.

## Decisions (locked)

| Topic | Choice |
|---|---|
| Enter to send | Optional `onSubmit` on `MentionTextarea`; Enter submits when mention picker is closed |
| Shift+Enter | Default newline (do not preventDefault) |
| Mention picker open | Enter / Tab still insert the highlighted mention (unchanged) |
| Empty send | Rely on existing `required` / form validation; do not submit empty content |
| Pointer cursor | Global CSS `button { cursor: pointer; }` in `index.css` |
| Auto-scroll | Track “near bottom” (~80px threshold) on the message log; scroll on message-list change only when sticky |
| Scroll target | The existing `overflow-auto` message log (`role="log"`) in conversation detail |

## Out of scope

- Changing send button layout or removing it
- Auto-scroll on non-conversation pages
- Keyboard shortcuts outside the chat composer
- Shared `Button` component / design-system refactor
- Mobile-specific keyboard quirks beyond standard Enter / Shift+Enter

## Frontend

### Composer (`MentionTextarea` + conversation form)

- Add optional `onSubmit?: () => void` to `MentionTextarea`.
- In `onKeyDown`, when the mention menu is **not** consuming the key:
  - `Enter` without `Shift` → `preventDefault()`, call `onSubmit` if provided.
  - `Shift+Enter` → leave default textarea newline behavior.
- Conversation detail form wires `onSubmit` to the existing send path (same as pressing Send), e.g. request form submit / call the same handler.
- Mention autocomplete key handling stays first: if picker is open with options, Enter selects mention and does **not** send.

### Sticky auto-scroll (conversation message log)

- Keep a ref on the scrollable message list.
- Maintain a boolean (or ref) `stickToBottom`, updated on `scroll` events:
  - `stickToBottom = scrollHeight - scrollTop - clientHeight <= threshold` (threshold ≈ 80px).
- When the rendered message list changes (new / replaced messages), if `stickToBottom` is true, set `scrollTop = scrollHeight` (or equivalent `scrollTo` / end sentinel).
- If the user has scrolled up beyond the threshold, do not move scroll on incoming messages until they return near the bottom.

### Button cursor

- In `frontend/src/index.css`, add:

```css
button {
  cursor: pointer;
}
```

- No per-button class churn; disabled buttons may still show `not-allowed` if/when `disabled` styling is added later (not required now).

## Testing

- Unit / component: Enter without Shift calls `onSubmit`; Shift+Enter does not; Enter with open mention menu inserts mention and does not call `onSubmit`.
- Manual / light UI check: scroll up in a long transcript → new message does not jump; scroll to bottom → new message follows; hover any button → pointer cursor.

## Success criteria

- Sending from the composer needs only Enter (when not selecting a mention).
- Multiline drafts use Shift+Enter.
- Reading older messages is not interrupted by new arrivals.
- Pointer cursor appears on all app buttons.
