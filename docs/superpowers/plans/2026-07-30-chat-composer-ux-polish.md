# Chat Composer UX Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enter sends chat messages (Shift+Enter for newline), sticky auto-scroll only when already at the bottom, and pointer cursor on all buttons.

**Architecture:** Extend `MentionTextarea` with optional `onSubmit` for Enter-to-send (mention picker still wins). Pure `isNearBottom` helper drives sticky scroll on the conversation message log. One global CSS rule for button cursors.

**Tech Stack:** React 19, TypeScript, Vite, Vitest, Testing Library, Tailwind CSS v4.

**Spec:** `docs/superpowers/specs/2026-07-30-chat-composer-ux-polish-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- Frontend under `frontend/` only — no backend changes.
- For C# (if touched): no primary constructors; do not inline object creation into method/ctor calls.
- Windows: no `.ps1`/`.sh`; commits via message file + `git commit -F`; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- UI copy / responses: English.

## File Structure

**Create**
- `frontend/src/areas/agents/scrollStickiness.ts` — pure near-bottom check + threshold constant
- `frontend/src/__tests__/scrollStickiness.test.ts`

**Modify**
- `frontend/src/areas/agents/MentionTextarea.tsx` — optional `onSubmit`; Enter / Shift+Enter
- `frontend/src/__tests__/MentionTextarea.test.tsx` — Enter / Shift+Enter / mention conflict cases
- `frontend/src/areas/agents/ConversationPages.tsx` — wire `onSubmit` via `requestSubmit`; sticky scroll on message log
- `frontend/src/index.css` — `button { cursor: pointer; }`

---

### Task 1: Enter sends; Shift+Enter newline in `MentionTextarea`

**Files:**
- Modify: `frontend/src/areas/agents/MentionTextarea.tsx`
- Modify: `frontend/src/__tests__/MentionTextarea.test.tsx`
- Modify: `frontend/src/areas/agents/ConversationPages.tsx` (composer form wiring only)

**Interfaces:**
- Consumes: existing `MentionTextarea` props (`value`, `onChange`, `participants`, …)
- Produces: `onSubmit?: () => void` on `MentionTextarea`; conversation form calls `form.requestSubmit()` from that callback

- [ ] **Step 1: Write the failing tests**

Extend `frontend/src/__tests__/MentionTextarea.test.tsx` — keep the existing harness test, add:

```tsx
import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MentionTextarea } from '../areas/agents/MentionTextarea'

const participants = [
  { agentId: 'id-leo', name: 'leo' },
  { agentId: 'id-max', name: 'max' },
]

function Harness({ onSubmit }: { onSubmit?: () => void }) {
  const [value, setValue] = useState('')
  return (
    <div>
      <MentionTextarea
        value={value}
        onChange={setValue}
        participants={participants}
        required
        onSubmit={onSubmit}
      />
      <output data-testid="value">{value}</output>
    </div>
  )
}

describe('MentionTextarea', () => {
  it('opens participant menu after typing @ and inserts on click', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hi @l')
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'leo' })).toBeInTheDocument()

    await user.click(screen.getByRole('option', { name: 'leo' }))
    expect(screen.getByTestId('value').textContent).toMatch(/@leo /)
  })

  it('calls onSubmit on Enter without Shift when mention menu is closed', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hello{Enter}')
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('does not call onSubmit on Shift+Enter', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hello{Shift>}{Enter}{/Shift}')
    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByTestId('value').textContent).toContain('\n')
  })

  it('inserts mention on Enter when menu is open and does not call onSubmit', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, '@l{Enter}')
    expect(screen.getByTestId('value').textContent).toMatch(/@leo /)
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cmd /c "cd frontend && npm test -- --run src/__tests__/MentionTextarea.test.tsx"`

Expected: FAIL — `onSubmit` prop / Enter-to-send behavior not implemented (TypeScript error and/or `onSubmit` never called / Shift+Enter newline missing).

- [ ] **Step 3: Implement Enter / Shift+Enter in `MentionTextarea`**

Update `Props`:

```ts
type Props = {
  value: string
  onChange: (next: string) => void
  participants: ParticipantDto[]
  required?: boolean
  className?: string
  onSubmit?: () => void
}
```

Update the component signature to accept `onSubmit`, and replace `onKeyDown` with:

```ts
function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
  if (open && options.length > 0) {
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      setHighlight((current) => (current + 1) % options.length)
      return
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault()
      setHighlight((current) => (current - 1 + options.length) % options.length)
      return
    }
    if (event.key === 'Enter' || event.key === 'Tab') {
      event.preventDefault()
      insertMention(options[highlight]!.name)
      return
    }
    if (event.key === 'Escape') {
      event.preventDefault()
      setOpen(false)
      return
    }
  }

  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    onSubmit?.()
  }
}
```

Leave Shift+Enter alone (no `preventDefault`) so the textarea inserts a newline.

- [ ] **Step 4: Wire conversation composer to `requestSubmit`**

In `frontend/src/areas/agents/ConversationPages.tsx`, inside `ConversationPage`:

1. Add `const formRef = useRef<HTMLFormElement>(null)` (import `useRef` if missing).
2. On the send `<form … onSubmit={(e) => void onSend(e)}>`, add `ref={formRef}`.
3. Pass to `MentionTextarea`:

```tsx
onSubmit={() => {
  formRef.current?.requestSubmit()
}}
```

`requestSubmit()` runs HTML `required` validation and invokes the existing `onSend` path — same as clicking Send.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cmd /c "cd frontend && npm test -- --run src/__tests__/MentionTextarea.test.tsx"`

Expected: PASS (all four cases).

- [ ] **Step 6: Commit**

```cmd
git add frontend/src/areas/agents/MentionTextarea.tsx frontend/src/__tests__/MentionTextarea.test.tsx frontend/src/areas/agents/ConversationPages.tsx
(
echo feat: Enter sends chat messages; Shift+Enter for newline
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 2: Sticky auto-scroll on conversation message log

**Files:**
- Create: `frontend/src/areas/agents/scrollStickiness.ts`
- Create: `frontend/src/__tests__/scrollStickiness.test.ts`
- Modify: `frontend/src/areas/agents/ConversationPages.tsx`

**Interfaces:**
- Consumes: message list from `messagesInOrder(state)`; scrollable `role="log"` div
- Produces:
  - `STICK_THRESHOLD_PX = 80`
  - `isNearBottom(scrollTop, clientHeight, scrollHeight, threshold?): boolean`
  - Conversation log scrolls to bottom on message changes only when stickiness is true

- [ ] **Step 1: Write the failing helper tests**

Create `frontend/src/__tests__/scrollStickiness.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { STICK_THRESHOLD_PX, isNearBottom } from '../areas/agents/scrollStickiness'

describe('isNearBottom', () => {
  it('is true when remaining distance is within the default threshold', () => {
    expect(isNearBottom(920, 100, 1000)).toBe(true)
    expect(isNearBottom(920 - STICK_THRESHOLD_PX, 100, 1000)).toBe(true)
  })

  it('is false when the user has scrolled up past the threshold', () => {
    expect(isNearBottom(920 - STICK_THRESHOLD_PX - 1, 100, 1000)).toBe(false)
    expect(isNearBottom(0, 100, 1000)).toBe(false)
  })

  it('respects a custom threshold', () => {
    // distance = 1000 - scrollTop - 100; threshold 50 → sticky when scrollTop >= 850
    expect(isNearBottom(849, 100, 1000, 50)).toBe(false)
    expect(isNearBottom(850, 100, 1000, 50)).toBe(true)
  })
})
```

Note: distance from bottom = `scrollHeight - scrollTop - clientHeight`. For `scrollTop=920`, `clientHeight=100`, `scrollHeight=1000` → distance `0`. For default threshold 80, sticky while `scrollTop >= 820`.

- [ ] **Step 2: Run helper tests to verify they fail**

Run: `cmd /c "cd frontend && npm test -- --run src/__tests__/scrollStickiness.test.ts"`

Expected: FAIL — module not found / `isNearBottom` undefined.

- [ ] **Step 3: Implement `scrollStickiness.ts`**

Create `frontend/src/areas/agents/scrollStickiness.ts`:

```ts
export const STICK_THRESHOLD_PX = 80

export function isNearBottom(
  scrollTop: number,
  clientHeight: number,
  scrollHeight: number,
  threshold: number = STICK_THRESHOLD_PX,
): boolean {
  return scrollHeight - scrollTop - clientHeight <= threshold
}
```

- [ ] **Step 4: Run helper tests to verify they pass**

Run: `cmd /c "cd frontend && npm test -- --run src/__tests__/scrollStickiness.test.ts"`

Expected: PASS.

- [ ] **Step 5: Wire sticky scroll in `ConversationPage`**

In `frontend/src/areas/agents/ConversationPages.tsx`:

1. Import `useEffect`, `useRef` as needed, plus:

```ts
import { isNearBottom } from './scrollStickiness'
```

2. Inside `ConversationPage`, add:

```ts
const logRef = useRef<HTMLDivElement>(null)
const stickToBottomRef = useRef(true)
```

3. On the message log div (`className="flex-1 space-y-3 overflow-auto"` / `role="log"`), add `ref={logRef}` and:

```tsx
onScroll={() => {
  const el = logRef.current
  if (!el) {
    return
  }
  stickToBottomRef.current = isNearBottom(el.scrollTop, el.clientHeight, el.scrollHeight)
}}
```

4. After deriving the ordered/filtered messages used for render (or using `state` / message count), add an effect that runs when the transcript changes:

```ts
const orderedMessages = messagesInOrder(state).filter((message) => message.role !== 'System')

useEffect(() => {
  if (!stickToBottomRef.current) {
    return
  }
  const el = logRef.current
  if (!el) {
    return
  }
  el.scrollTop = el.scrollHeight
}, [orderedMessages])
```

If extracting `orderedMessages` inline is awkward for the dependency array, depend on a stable signal such as `orderedMessages.map((m) => m.id).join(',')` or `orderedMessages.length` **plus** last message id — prefer depending on the message id list so content updates that keep length also scroll when sticky:

```ts
const messageIds = orderedMessages.map((message) => message.id).join(',')

useEffect(() => {
  if (!stickToBottomRef.current) {
    return
  }
  const el = logRef.current
  if (!el) {
    return
  }
  el.scrollTop = el.scrollHeight
}, [messageIds])
```

Use `orderedMessages` for the `.map` in JSX (do not call `messagesInOrder` twice with different filters).

- [ ] **Step 6: Typecheck / run related tests**

Run: `cmd /c "cd frontend && npm test -- --run src/__tests__/scrollStickiness.test.ts src/__tests__/MentionTextarea.test.tsx"`

Run: `cmd /c "cd frontend && npm run lint"`

Expected: PASS / no type errors.

- [ ] **Step 7: Commit**

```cmd
git add frontend/src/areas/agents/scrollStickiness.ts frontend/src/__tests__/scrollStickiness.test.ts frontend/src/areas/agents/ConversationPages.tsx
(
echo feat: sticky auto-scroll conversation log when already at bottom
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 3: Pointer cursor on all buttons

**Files:**
- Modify: `frontend/src/index.css`

**Interfaces:**
- Consumes: existing Tailwind import / `:root` styles
- Produces: global `button { cursor: pointer; }`

- [ ] **Step 1: Add global button cursor rule**

In `frontend/src/index.css`, after the `body { … }` block, add:

```css
button {
  cursor: pointer;
}
```

- [ ] **Step 2: Sanity-check CSS is imported**

Confirm `frontend/src/main.tsx` (or entry) still imports `./index.css`. No code change needed if already present.

- [ ] **Step 3: Commit**

```cmd
git add frontend/src/index.css
(
echo feat: show pointer cursor on all buttons
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

## Manual verification (after all tasks)

1. Open a conversation, type a message, press Enter → sends.
2. Shift+Enter → newline in composer; does not send.
3. Type `@` + filter, press Enter → inserts mention; does not send.
4. With a long transcript, scroll up; wait for / trigger a new message → view does not jump.
5. Scroll to bottom; new message → stays pinned to end.
6. Hover any button (Send, Draft run, list actions) → pointer cursor.

---

## Spec coverage checklist

| Spec requirement | Task |
|---|---|
| Enter sends when mention menu closed | Task 1 |
| Shift+Enter newline | Task 1 |
| Enter with open mention menu inserts mention | Task 1 |
| Empty send via `required` / form validation | Task 1 (`requestSubmit`) |
| Sticky auto-scroll ~80px threshold | Task 2 |
| Global `button { cursor: pointer }` | Task 3 |
