import { useEffect, useRef, useState } from 'react'
import { patchConversationTitle } from './api'
import type { TitleMode } from './types'

type TitleUpdate = {
  title: string
  titleMode: TitleMode
  concurrencyToken: string
}

type Props = {
  conversationId: string
  title: string
  titleMode: TitleMode
  concurrencyToken: string
  onUpdated: (next: TitleUpdate) => void
  onEditingChange?: (editing: boolean) => void
}

export function ConversationTitleHeader({
  conversationId,
  title,
  titleMode,
  concurrencyToken,
  onUpdated,
  onEditingChange,
}: Props) {
  const [draft, setDraft] = useState(title)
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (!editing) {
      setDraft(title)
    }
  }, [title, editing])

  useEffect(() => {
    onEditingChange?.(editing)
  }, [editing, onEditingChange])

  const setEditingState = (value: boolean) => {
    setEditing(value)
  }

  const saveTitle = async () => {
    const next = draft.trim()
    if (!next || next === title) {
      setDraft(title)
      setEditingState(false)
      return
    }

    setBusy(true)
    try {
      const updated = await patchConversationTitle(conversationId, {
        action: 'set',
        title: next,
        concurrencyToken,
      })
      onUpdated({
        title: updated.title,
        titleMode: updated.titleMode,
        concurrencyToken: updated.concurrencyToken,
      })
      setEditingState(false)
    } finally {
      setBusy(false)
    }
  }

  const onModeClick = async () => {
    setBusy(true)
    try {
      const action = titleMode === 'auto' ? 'lock' : 'resume'
      const updated = await patchConversationTitle(conversationId, {
        action,
        concurrencyToken,
      })
      onUpdated({
        title: updated.title,
        titleMode: updated.titleMode,
        concurrencyToken: updated.concurrencyToken,
      })
    } finally {
      setBusy(false)
    }
  }

  const modeLabel = titleMode === 'auto' ? 'OK' : 'Auto'

  return (
    <div className="flex items-center gap-2">
      <input
        ref={inputRef}
        className="min-w-0 flex-1 border-b border-transparent bg-transparent text-xl font-semibold outline-none focus:border-[var(--accent)]"
        value={draft}
        disabled={busy}
        aria-label="Conversation title"
        onFocus={() => setEditingState(true)}
        onChange={(event) => setDraft(event.target.value)}
        onBlur={() => {
          void saveTitle()
        }}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault()
            inputRef.current?.blur()
          }
          if (event.key === 'Escape') {
            setDraft(title)
            setEditingState(false)
            inputRef.current?.blur()
          }
        }}
      />
      <button
        type="button"
        className="shrink-0 rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white disabled:opacity-50"
        disabled={busy}
        onClick={() => {
          void onModeClick()
        }}
      >
        {modeLabel}
      </button>
    </div>
  )
}
