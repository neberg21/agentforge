import { useMemo, useRef, useState, type KeyboardEvent } from 'react'
import type { ParticipantDto } from './types'

type Props = {
  value: string
  onChange: (next: string) => void
  participants: ParticipantDto[]
  required?: boolean
  className?: string
  onSubmit?: () => void
}

type ActiveQuery = {
  start: number
  query: string
}

function findActiveQuery(value: string, caret: number): ActiveQuery | null {
  const before = value.slice(0, caret)
  const match = /@([\w-]*)$/.exec(before)
  if (!match) {
    return null
  }
  return { start: match.index, query: match[1] ?? '' }
}

export function MentionTextarea({
  value,
  onChange,
  participants,
  required,
  className,
  onSubmit,
}: Props) {
  const ref = useRef<HTMLTextAreaElement>(null)
  const [caret, setCaret] = useState(0)
  const [open, setOpen] = useState(false)
  const [highlight, setHighlight] = useState(0)

  const active = useMemo(() => findActiveQuery(value, caret), [value, caret])
  const options = useMemo(() => {
    if (!active) {
      return []
    }
    const q = active.query.toLowerCase()
    return participants.filter((participant) => participant.name.toLowerCase().startsWith(q))
  }, [active, participants])

  function syncCaret() {
    const el = ref.current
    if (!el) {
      return
    }
    const nextCaret = el.selectionStart ?? 0
    setCaret(nextCaret)
    const query = findActiveQuery(value, nextCaret)
    setOpen(query !== null)
    setHighlight(0)
  }

  function insertMention(name: string) {
    if (!active) {
      return
    }
    const before = value.slice(0, active.start)
    const after = value.slice(caret)
    const inserted = `@${name} `
    const next = `${before}${inserted}${after}`
    onChange(next)
    setOpen(false)
    const nextCaret = before.length + inserted.length
    requestAnimationFrame(() => {
      const el = ref.current
      if (!el) {
        return
      }
      el.focus()
      el.setSelectionRange(nextCaret, nextCaret)
      setCaret(nextCaret)
    })
  }

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

  return (
    <div className="relative">
      <textarea
        ref={ref}
        className={className}
        value={value}
        required={required}
        onChange={(event) => {
          onChange(event.target.value)
          const nextCaret = event.target.selectionStart ?? event.target.value.length
          setCaret(nextCaret)
          const query = findActiveQuery(event.target.value, nextCaret)
          setOpen(query !== null)
          setHighlight(0)
        }}
        onClick={syncCaret}
        onKeyUp={syncCaret}
        onKeyDown={onKeyDown}
      />
      {open && options.length > 0 ? (
        <ul
          role="listbox"
          className="absolute bottom-full z-10 mb-1 max-h-40 w-full overflow-auto rounded border border-[var(--border)] bg-[var(--panel)] text-sm shadow"
        >
          {options.map((participant, index) => (
            <li key={participant.agentId}>
              <button
                type="button"
                role="option"
                aria-selected={index === highlight}
                className={`block w-full px-3 py-1.5 text-left ${
                  index === highlight ? 'bg-[var(--border)]' : ''
                }`}
                onMouseDown={(event) => {
                  event.preventDefault()
                  insertMention(participant.name)
                }}
              >
                {participant.name}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
