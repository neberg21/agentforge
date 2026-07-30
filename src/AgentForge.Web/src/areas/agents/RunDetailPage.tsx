import { useEffect, useReducer, useState } from 'react'
import { Link, useParams } from 'react-router'
import { cancelRun, getRun, getRunMessages } from './api'
import type { RunDto } from './types'
import { openEventSource } from '../../lib/sse'
import {
  emptyTranscript,
  messagesInOrder,
  transcriptReducer,
  type TranscriptMessage,
} from './transcriptReducer'
import { rememberItem } from '../../lib/recent'
import { useContextPanel } from '../../shell/ContextPanel'
import type { ApiError } from '../../lib/http'
import { ToolCallCard } from './ToolCallCard'

function toTranscript(messages: Awaited<ReturnType<typeof getRunMessages>>): TranscriptMessage[] {
  return messages.map((message) => ({
    id: message.id,
    sequence: message.sequence,
    role: message.role,
    content: message.content,
    toolCallsJson: message.toolCallsJson,
    toolCallId: message.toolCallId,
  }))
}

export function RunDetailPage() {
  const { id } = useParams()
  const { setContent } = useContextPanel()
  const [run, setRun] = useState<RunDto | null>(null)
  const [logMode, setLogMode] = useState(false)
  const [state, dispatch] = useReducer(transcriptReducer, undefined, emptyTranscript)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      return
    }
    let closed = false
    let stopStream: (() => void) | undefined

    async function load() {
      const [runValue, messages] = await Promise.all([getRun(id!), getRunMessages(id!)])
      if (closed) {
        return
      }
      setRun(runValue)
      rememberItem({ kind: 'run', id: runValue.id, label: runValue.objective.slice(0, 40) })
      dispatch({ type: 'hydrate', messages: toTranscript(messages) })
      stopStream = openEventSource(`/api/agents/runs/${id}/stream`, {
        onEvent: (type, data) => dispatch({ type: 'sse', event: type, data }),
        onError: () => setError('Stream disconnected'),
      })
    }

    void load()
    return () => {
      closed = true
      stopStream?.()
    }
  }, [id])

  useEffect(() => {
    if (!state.needsMessageReload || !id) {
      return
    }
    void getRunMessages(id).then((messages) => {
      dispatch({ type: 'reloadMessages', messages: toTranscript(messages) })
    })
  }, [state.needsMessageReload, id])

  useEffect(() => {
    if (!run) {
      setContent(null)
      return
    }
    setContent(
      <div className="space-y-2 text-sm">
        <div className="font-semibold">Run</div>
        <div>Status: {state.status ?? run.status}</div>
        <div>Agent: {run.agentSnapshot.name}</div>
        {run.conversationId ? (
          <Link className="underline" to={`/agents/conversations/${run.conversationId}`}>
            Open conversation
          </Link>
        ) : null}
        {state.usage ? (
          <div>
            Tokens: {state.usage.promptTokens ?? run.promptTokens}/
            {state.usage.completionTokens ?? run.completionTokens}
          </div>
        ) : null}
      </div>,
    )
    return () => setContent(null)
  }, [run, state, setContent])

  if (!run) {
    return <p>Loading…</p>
  }

  const messages = messagesInOrder(state)

  return (
    <div className="flex h-full flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">{run.objective}</h1>
        <div className="flex gap-2 text-sm">
          <button type="button" className="underline" onClick={() => setLogMode((value) => !value)}>
            {logMode ? 'Chat view' : 'Log view'}
          </button>
          {(run.status === 'Pending' || run.status === 'Running') && (
            <button
              type="button"
              className="underline"
              onClick={() => {
                void cancelRun(run.id, run.concurrencyToken)
                  .then(setRun)
                  .catch((err: ApiError) => setError(err.detail ?? err.title))
              }}
            >
              Cancel
            </button>
          )}
        </div>
      </div>
      {error ? <p className="text-sm text-red-600">{error}</p> : null}
      <div className="flex-1 space-y-3 overflow-auto" role="log" aria-live="polite">
        {messages.map((message) => (
          <div key={message.id} className="rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm">
            {logMode ? (
              <div>
                #{message.sequence} {message.role}
                <pre className="mt-1 whitespace-pre-wrap">{message.content}</pre>
                {message.toolCallsJson ? (
                  <pre className="mt-1 whitespace-pre-wrap text-[var(--muted)]">{message.toolCallsJson}</pre>
                ) : null}
              </div>
            ) : (
              <div>
                <div className="mb-1 text-xs uppercase text-[var(--muted)]">{message.role}</div>
                <div className="whitespace-pre-wrap">{message.content}</div>
                <ToolCallCard toolCallsJson={message.toolCallsJson} />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
