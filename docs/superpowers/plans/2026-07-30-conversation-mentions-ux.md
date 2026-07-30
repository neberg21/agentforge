# Conversation Mentions UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `@` autocomplete in conversation composers, always auto-address the agent in 1:1 chats (configurable prepend/append constant), and show who user messages were addressed to in the transcript.

**Architecture:** Keep POST `{ content, mentions[] }` unchanged. Parse and 1:1 auto-insert on the client. Expose stored `MentionsJson` as `mentions` on message GET. Pure helpers for parse/auto-insert/display; a small `MentionTextarea`; wire into `ConversationPages`.

**Tech Stack:** .NET 10 / xUnit v3 (backend DTO); React 19, TypeScript, Vite, Vitest, Testing Library (frontend).

**Spec:** `docs/superpowers/specs/2026-07-30-conversation-mentions-ux-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- No C# primary constructors; do not inline object creation into method/ctor calls.
- No DB migration; only map existing `MentionsJson`.
- Frontend lives under `frontend/` (not `src/AgentForge.Web/`).
- Windows: no `.ps1`/`.sh`; commits via `git commit -F %TEMP%\commitmsg.txt`; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- Response language / UI copy: English (existing conversation UI is English).

## File Structure

**Backend — modify**
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs` — add `Mentions` to `ConversationMessageResponse`
- `backend/tests/AgentForge.Areas.Agents.Unit/ConversationMessageResponseTests.cs` — create

**Frontend — create**
- `frontend/src/areas/agents/mentionConfig.ts`
- `frontend/src/areas/agents/mentions.ts`
- `frontend/src/areas/agents/MentionTextarea.tsx`
- `frontend/src/areas/agents/MessageBody.tsx`
- `frontend/src/__tests__/mentions.test.ts`
- `frontend/src/__tests__/MessageBody.test.tsx`
- `frontend/src/__tests__/MentionTextarea.test.tsx`

**Frontend — modify**
- `frontend/src/areas/agents/types.ts` — `mentions` on `ConversationMessageDto`
- `frontend/src/areas/agents/transcriptReducer.ts` — carry `mentions` on `TranscriptMessage`
- `frontend/src/areas/agents/ConversationPages.tsx` — composer + transcript wiring; drop chip row; catch send errors

---

### Task 1: Expose `mentions` on conversation message API

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/ConversationMessageResponseTests.cs`

**Interfaces:**
- Consumes: `ConversationMessage.MentionsJson` (`string?`, JSON array of Guids)
- Produces: `ConversationMessageResponse.Mentions` as `Guid[]?` (null when no JSON / empty)

- [ ] **Step 1: Write the failing test**

Create `backend/tests/AgentForge.Areas.Agents.Unit/ConversationMessageResponseTests.cs`:

```csharp
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Http;

namespace AgentForge.Areas.Agents.Unit;

public class ConversationMessageResponseTests
{
    [Fact]
    public void From_WhenMentionsJsonPresent_MapsGuidArray()
    {
        var ownerId = "owner-1";
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var participantIds = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create(ownerId, "c", participantIds, now);
        var agentId = Guid.CreateVersion7();
        var mentionsJson = $"[\"{agentId}\"]";
        var message = conversation.AppendMessage(
            MessageRole.User,
            "hi",
            now,
            senderAgentId: null,
            senderName: null,
            mentionsJson,
            toolCallsJson: null,
            toolCallId: null);

        var response = ConversationMessageResponse.From(message);

        Assert.NotNull(response.Mentions);
        Assert.Equal(agentId, response.Mentions![0]);
    }

    [Fact]
    public void From_WhenMentionsJsonNull_MapsNullMentions()
    {
        var ownerId = "owner-1";
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var participantIds = new[] { Guid.CreateVersion7() };
        var conversation = Conversation.Create(ownerId, "c", participantIds, now);
        var message = conversation.AppendMessage(
            MessageRole.User,
            "note",
            now,
            senderAgentId: null,
            senderName: null,
            mentionsJson: null,
            toolCallsJson: null,
            toolCallId: null);

        var response = ConversationMessageResponse.From(message);

        Assert.Null(response.Mentions);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```cmd
dotnet test backend\tests\AgentForge.Areas.Agents.Unit\AgentForge.Areas.Agents.Unit.csproj --filter ConversationMessageResponseTests
```

Expected: FAIL (compile error: `Mentions` does not exist on `ConversationMessageResponse`).

- [ ] **Step 3: Write minimal implementation**

Update `ConversationMessageResponse` in `Responses.cs` to add `Guid[]? Mentions` and map it in `From`:

```csharp
public sealed record ConversationMessageResponse(
    Guid Id,
    int Sequence,
    string Role,
    string? Content,
    string? ToolCallsJson,
    string? ToolCallId,
    Guid? SenderAgentId,
    string? SenderName,
    DateTimeOffset CreatedAt,
    Guid[]? Mentions)
{
    public static ConversationMessageResponse From(ConversationMessage message)
    {
        Guid[]? mentions = null;
        if (!string.IsNullOrWhiteSpace(message.MentionsJson))
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(message.MentionsJson);
            mentions = parsed is { Length: > 0 } ? parsed : null;
        }

        return new ConversationMessageResponse(
            message.Id,
            message.Sequence,
            message.Role.ToString(),
            message.Content,
            message.ToolCallsJson,
            message.ToolCallId,
            message.SenderAgentId,
            message.SenderName,
            message.CreatedAt,
            mentions);
    }
}
```

JSON serialization already camelCases → `mentions` for the SPA.

- [ ] **Step 4: Run test to verify it passes**

Run the same `dotnet test` filter. Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
git add backend\src\Areas\AgentForge.Areas.Agents\Http\Responses.cs backend\tests\AgentForge.Areas.Agents.Unit\ConversationMessageResponseTests.cs
(
echo feat: expose conversation message mentions on API responses
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 2: Mention helpers + config (parse, auto-insert, missing-from-text)

**Files:**
- Create: `frontend/src/areas/agents/mentionConfig.ts`
- Create: `frontend/src/areas/agents/mentions.ts`
- Create: `frontend/src/__tests__/mentions.test.ts`

**Interfaces:**
- Consumes: `ParticipantDto` `{ agentId: string; name: string }`
- Produces:
  - `autoMentionPosition: 'prepend' | 'append'` (default `'prepend'`)
  - `parseMentions(text: string, participants: ParticipantDto[]): string[]` — agent IDs, first-appearance order, longest-name wins
  - `ensureAutoMention(text: string, participant: ParticipantDto, position: 'prepend' | 'append'): string`
  - `mentionsMissingFromText(text: string, mentionIds: string[], participants: ParticipantDto[]): ParticipantDto[]`

- [ ] **Step 1: Write the failing test**

Create `frontend/src/__tests__/mentions.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import {
  ensureAutoMention,
  mentionsMissingFromText,
  parseMentions,
} from '../areas/agents/mentions'
import type { ParticipantDto } from '../areas/agents/types'

const leo: ParticipantDto = { agentId: 'id-leo', name: 'leo' }
const max: ParticipantDto = { agentId: 'id-max', name: 'max' }
const leoBot: ParticipantDto = { agentId: 'id-leobot', name: 'leoBot' }

describe('parseMentions', () => {
  it('finds a single mention case-insensitively', () => {
    expect(parseMentions('hey @Leo please', [leo, max])).toEqual(['id-leo'])
  })

  it('prefers the longest matching name at a position', () => {
    expect(parseMentions('hi @leoBot', [leo, leoBot])).toEqual(['id-leobot'])
  })

  it('ignores unknown @tokens and dedupes', () => {
    expect(parseMentions('@leo @ghost @leo', [leo])).toEqual(['id-leo'])
  })

  it('returns empty when none', () => {
    expect(parseMentions('plain note', [leo])).toEqual([])
  })
})

describe('ensureAutoMention', () => {
  it('prepends @Name by default formatting', () => {
    expect(ensureAutoMention('hello', leo, 'prepend')).toBe('@leo hello')
  })

  it('appends @Name', () => {
    expect(ensureAutoMention('hello', leo, 'append')).toBe('hello @leo')
  })

  it('append on empty is just @Name', () => {
    expect(ensureAutoMention('', leo, 'append')).toBe('@leo')
  })
})

describe('mentionsMissingFromText', () => {
  it('returns participants whose @Name is absent from text', () => {
    const missing = mentionsMissingFromText('please look', ['id-leo'], [leo])
    expect(missing).toEqual([leo])
  })

  it('returns empty when @Name already in text', () => {
    const missing = mentionsMissingFromText('@leo please', ['id-leo'], [leo])
    expect(missing).toEqual([])
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```cmd
cd frontend
npm test -- src/__tests__/mentions.test.ts
```

Expected: FAIL (module not found).

- [ ] **Step 3: Write minimal implementation**

`frontend/src/areas/agents/mentionConfig.ts`:

```ts
export type AutoMentionPosition = 'prepend' | 'append'

export const autoMentionPosition: AutoMentionPosition = 'prepend'
```

`frontend/src/areas/agents/mentions.ts`:

```ts
import type { AutoMentionPosition } from './mentionConfig'
import type { ParticipantDto } from './types'

export function parseMentions(text: string, participants: ParticipantDto[]): string[] {
  const sorted = [...participants].sort((a, b) => b.name.length - a.name.length)
  const found: string[] = []
  const seen = new Set<string>()
  let i = 0
  while (i < text.length) {
    if (text[i] !== '@') {
      i += 1
      continue
    }
    const after = text.slice(i + 1)
    let matched: ParticipantDto | null = null
    for (const participant of sorted) {
      if (after.toLowerCase().startsWith(participant.name.toLowerCase())) {
        const end = participant.name.length
        const boundary = after[end]
        if (boundary === undefined || /[\s.,!?;:]/.test(boundary)) {
          matched = participant
          break
        }
      }
    }
    if (matched) {
      if (!seen.has(matched.agentId)) {
        seen.add(matched.agentId)
        found.push(matched.agentId)
      }
      i += 1 + matched.name.length
    } else {
      i += 1
    }
  }
  return found
}

export function ensureAutoMention(
  text: string,
  participant: ParticipantDto,
  position: AutoMentionPosition,
): string {
  const token = `@${participant.name}`
  if (position === 'prepend') {
    const rest = text.trimStart()
    return rest.length === 0 ? token : `${token} ${rest}`
  }
  const rest = text.trimEnd()
  return rest.length === 0 ? token : `${rest} ${token}`
}

export function mentionsMissingFromText(
  text: string,
  mentionIds: string[],
  participants: ParticipantDto[],
): ParticipantDto[] {
  const byId = new Map(participants.map((p) => [p.agentId, p]))
  const missing: ParticipantDto[] = []
  for (const id of mentionIds) {
    const participant = byId.get(id)
    if (!participant) {
      continue
    }
    const needle = `@${participant.name}`.toLowerCase()
    if (!text.toLowerCase().includes(needle)) {
      missing.push(participant)
    }
  }
  return missing
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Vitest command. Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
git add frontend\src\areas\agents\mentionConfig.ts frontend\src\areas\agents\mentions.ts frontend\src\__tests__\mentions.test.ts
(
echo feat: add conversation mention parse and auto-insert helpers
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 3: Message body rendering (highlight + conditional To chips)

**Files:**
- Create: `frontend/src/areas/agents/MessageBody.tsx`
- Create: `frontend/src/__tests__/MessageBody.test.tsx`
- Modify: `frontend/src/areas/agents/types.ts` — add `mentions: string[] | null` to `ConversationMessageDto`
- Modify: `frontend/src/areas/agents/transcriptReducer.ts` — add optional `mentions?: string[] | null` to `TranscriptMessage`

**Interfaces:**
- Consumes: `mentionsMissingFromText`, `ParticipantDto`, message fields
- Produces: `<MessageBody content mentions participants role />` React component

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MessageBody } from '../areas/agents/MessageBody'

const leo = { agentId: 'id-leo', name: 'leo' }

describe('MessageBody', () => {
  it('highlights @Name and omits To chip when text contains the mention', () => {
    render(
      <MessageBody
        role="User"
        content="@leo please review"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.getByText('@leo')).toBeInTheDocument()
    expect(screen.queryByText(/To:/)).toBeNull()
  })

  it('shows To chip when mention ids exist but @Name is absent', () => {
    render(
      <MessageBody
        role="User"
        content="please review"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.getByText('To: @leo')).toBeInTheDocument()
  })

  it('shows no To chips for assistant messages', () => {
    render(
      <MessageBody
        role="Assistant"
        content="ok"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.queryByText(/To:/)).toBeNull()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```cmd
cd frontend
npm test -- src/__tests__/MessageBody.test.tsx
```

Expected: FAIL (module not found).

- [ ] **Step 3: Write minimal implementation**

Update `ConversationMessageDto` in `types.ts`:

```ts
export type ConversationMessageDto = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  senderAgentId: string | null
  senderName: string | null
  mentions: string[] | null
  createdAt: string
}
```

Add to `TranscriptMessage` in `transcriptReducer.ts`:

```ts
mentions?: string[] | null
```

Create `MessageBody.tsx`:

```tsx
import type { ReactNode } from 'react'
import { mentionsMissingFromText } from './mentions'
import type { ParticipantDto } from './types'

type Props = {
  role: string
  content: string | null
  mentions: string[] | null | undefined
  participants: ParticipantDto[]
}

function highlightContent(content: string, participants: ParticipantDto[]): ReactNode[] {
  const sorted = [...participants].sort((a, b) => b.name.length - a.name.length)
  const nodes: ReactNode[] = []
  let i = 0
  let key = 0
  while (i < content.length) {
    if (content[i] !== '@') {
      let j = i + 1
      while (j < content.length && content[j] !== '@') j += 1
      nodes.push(<span key={key++}>{content.slice(i, j)}</span>)
      i = j
      continue
    }
    const after = content.slice(i + 1)
    let matched: ParticipantDto | null = null
    for (const participant of sorted) {
      if (after.toLowerCase().startsWith(participant.name.toLowerCase())) {
        const end = participant.name.length
        const boundary = after[end]
        if (boundary === undefined || /[\s.,!?;:]/.test(boundary)) {
          matched = participant
          break
        }
      }
    }
    if (matched) {
      nodes.push(
        <span key={key++} className="rounded bg-[var(--border)] px-0.5 text-[var(--accent)]">
          @{matched.name}
        </span>,
      )
      i += 1 + matched.name.length
    } else {
      nodes.push(<span key={key++}>@</span>)
      i += 1
    }
  }
  return nodes
}

export function MessageBody({ role, content, mentions, participants }: Props) {
  const text = content ?? ''
  const mentionIds = mentions ?? []
  const showTo =
    role === 'User' && mentionIds.length > 0
      ? mentionsMissingFromText(text, mentionIds, participants)
      : []

  return (
    <div>
      {showTo.length > 0 ? (
        <div className="mb-1 flex flex-wrap gap-1">
          {showTo.map((participant) => (
            <span
              key={participant.agentId}
              className="rounded-full border border-[var(--border)] px-2 py-0.5 text-xs text-[var(--muted)]"
            >
              To: @{participant.name}
            </span>
          ))}
        </div>
      ) : null}
      <div className="whitespace-pre-wrap">{highlightContent(text, participants)}</div>
    </div>
  )
}
```

- [ ] **Step 4: Run test to verify it passes**

Same Vitest command. Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
git add frontend\src\areas\agents\MessageBody.tsx frontend\src\__tests__\MessageBody.test.tsx frontend\src\areas\agents\types.ts frontend\src\areas\agents\transcriptReducer.ts
(
echo feat: render conversation mention highlights and To chips
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 4: `MentionTextarea` (@ autocomplete)

**Files:**
- Create: `frontend/src/areas/agents/MentionTextarea.tsx`
- Create: `frontend/src/__tests__/MentionTextarea.test.tsx`

**Interfaces:**
- Consumes: `participants: ParticipantDto[]`, controlled `value` / `onChange`
- Produces: textarea + dropdown; selecting or Enter/Tab inserts `@Name ` replacing the active `@query`

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MentionTextarea } from '../areas/agents/MentionTextarea'

const participants = [
  { agentId: 'id-leo', name: 'leo' },
  { agentId: 'id-max', name: 'max' },
]

describe('MentionTextarea', () => {
  it('opens participant menu after typing @ and inserts on click', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<MentionTextarea value="" onChange={onChange} participants={participants} required />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hi @l')
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'leo' })).toBeInTheDocument()

    await user.click(screen.getByRole('option', { name: 'leo' }))
    expect(onChange).toHaveBeenCalled()
    const last = onChange.mock.calls.at(-1)?.[0] as string
    expect(last).toMatch(/@leo /)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```cmd
cd frontend
npm test -- src/__tests__/MentionTextarea.test.tsx
```

Expected: FAIL (module not found).

- [ ] **Step 3: Write minimal implementation**

Implement `MentionTextarea.tsx` with:

- Track caret; detect active query as `/@([\w-]*)$/` before caret (ASCII names matching current agents; filter `participants` by `name.toLowerCase().startsWith(query)`).
- `listbox` / `option` roles for a11y.
- ArrowUp/ArrowDown change highlight; Enter/Tab insert; Esc closes.
- `insertMention(name)` replaces from `@` start through query with `@${name} `.
- Props: `value`, `onChange(next: string)`, `participants`, plus pass-through `required` / `className`.

Keep the component under ~120 lines; no external mention library.

- [ ] **Step 4: Run test to verify it passes**

Same Vitest command. Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
git add frontend\src\areas\agents\MentionTextarea.tsx frontend\src\__tests__\MentionTextarea.test.tsx
(
echo feat: add @ autocomplete MentionTextarea for conversations
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 5: Wire `ConversationPage` (send path + transcript)

**Files:**
- Modify: `frontend/src/areas/agents/ConversationPages.tsx`

**Interfaces:**
- Consumes: `autoMentionPosition`, `parseMentions`, `ensureAutoMention`, `MentionTextarea`, `MessageBody`
- Produces: working conversation UX per acceptance criteria in the spec

- [ ] **Step 1: Write / extend a focused behavior test (optional page-level)**

Prefer a small pure test of the send-prep function extracted if useful:

```ts
// in mentions.test.ts or conversationSend.test.ts
import { prepareOutgoingMessage } from '../areas/agents/mentions'
```

Add to `mentions.ts`:

```ts
export function prepareOutgoingMessage(
  content: string,
  participants: ParticipantDto[],
  position: AutoMentionPosition,
): { content: string; mentions: string[] } {
  let text = content
  let mentions = parseMentions(text, participants)
  if (participants.length === 1 && mentions.length === 0) {
    text = ensureAutoMention(text, participants[0], position)
    mentions = parseMentions(text, participants)
  }
  return { content: text, mentions }
}
```

Add tests:

```ts
it('prepareOutgoingMessage auto-addresses 1:1', () => {
  const result = prepareOutgoingMessage('hello', [leo], 'prepend')
  expect(result.content).toBe('@leo hello')
  expect(result.mentions).toEqual(['id-leo'])
})

it('prepareOutgoingMessage leaves multi-agent notes alone', () => {
  const result = prepareOutgoingMessage('hello', [leo, max], 'prepend')
  expect(result.content).toBe('hello')
  expect(result.mentions).toEqual([])
})
```

- [ ] **Step 2: Run tests — expect fail then implement `prepareOutgoingMessage`**

- [ ] **Step 3: Wire `ConversationPages.tsx`**

Changes:

1. Remove `mentions` state and chip row UI.
2. Replace `<textarea>` with `<MentionTextarea value={content} onChange={setMessage} participants={conversation.participants} … />`.
3. In `onSend`:

```ts
try {
  const prepared = prepareOutgoingMessage(content, conversation.participants, autoMentionPosition)
  setHint(
    conversation.participants.length > 1 && prepared.mentions.length === 0
      ? 'Not addressed — no agent will reply.'
      : null,
  )
  await postConversationMessage(id, { content: prepared.content, mentions: prepared.mentions })
  setMessage('')
  // reload messages as today, mapping mentions onto TranscriptMessage
} catch (error) {
  setHint(typeof error === 'object' && error && 'detail' in error
    ? String((error as { detail?: string }).detail ?? 'Send failed')
    : 'Send failed')
}
```

4. Map `mentions` through hydrate / reload / post-reload paths.
5. Replace plain `message.content` with:

```tsx
<MessageBody
  role={message.role}
  content={message.content}
  mentions={message.mentions}
  participants={conversation.participants}
/>
```

- [ ] **Step 4: Run frontend tests**

```cmd
cd frontend
npm test
npm run lint
```

Expected: all PASS / tsc clean.

- [ ] **Step 5: Commit**

```cmd
git add frontend\src\areas\agents\ConversationPages.tsx frontend\src\areas\agents\mentions.ts frontend\src\__tests__\mentions.test.ts
(
echo feat: wire mention autocomplete and 1:1 auto-address in conversations
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 6: Manual acceptance check

**Files:** none (verification only)

- [ ] **Step 1: Restart backend + `npm run dev`**

- [ ] **Step 2: Verify acceptance from the spec**

1. 1:1 chat: type `hello` without `@` → send → message shows `@leo hello` (or configured position), agent replies.
2. Type `@` → menu → pick agent → inserts `@Name `.
3. Transcript: highlighted `@`; To chip only if mention ids without `@` in text (e.g. older messages).
4. Multi-agent: send without `@` → note + hint; no reply.
5. Network tab: GET messages includes `mentions` array for addressed user messages.

- [ ] **Step 3: No commit unless fixes were needed**

---

## Spec coverage self-review

| Spec requirement | Task |
|---|---|
| Expose `mentions` on GET messages | Task 1 |
| Remove chips; `@` autocomplete | Tasks 4–5 |
| Parse on send; 1:1 auto-insert + position constant | Tasks 2, 5 |
| Highlight + conditional To chips | Task 3 |
| Multi-agent note + hint | Task 5 |
| Vitest helpers / light UI tests | Tasks 2–4 |
| Backend mapping test | Task 1 |
| Catch send errors | Task 5 |
| Manual acceptance | Task 6 |

No placeholders left after self-review. Name-matching boundary regex is specified in Task 2 code.
