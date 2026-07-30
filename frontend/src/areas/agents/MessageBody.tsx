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
      while (j < content.length && content[j] !== '@') {
        j += 1
      }
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
