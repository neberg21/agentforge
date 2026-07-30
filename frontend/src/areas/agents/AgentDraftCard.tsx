import { useState } from 'react'
import { Link } from 'react-router'
import { createAgent } from './api'
import type { ValidAgentDraft } from './agentDraft'
import { toCreateAgentBody } from './agentDraft'
import type { ApiError } from '../../lib/http'

type Props = {
  messageId: string
  draft: ValidAgentDraft
  createdAgentId: string | null
  onCreated: (messageId: string, agentId: string) => void
}

export function AgentDraftCard({
  messageId,
  draft,
  createdAgentId,
  onCreated,
}: Props) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (createdAgentId) {
    return (
      <div className="mt-2 rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm">
        <div className="font-medium">Created: {draft.name}</div>
        <Link className="underline" to={`/agents/definitions/${createdAgentId}`}>
          Open agent
        </Link>
      </div>
    )
  }

  return (
    <div className="mt-2 rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm">
      <div className="mb-2 font-medium">Proposed agent</div>
      <dl className="space-y-1">
        <div>
          <dt className="text-xs text-[var(--muted)]">Name</dt>
          <dd>{draft.name}</dd>
        </div>
        {draft.description ? (
          <div>
            <dt className="text-xs text-[var(--muted)]">Description</dt>
            <dd>{draft.description}</dd>
          </div>
        ) : null}
        <div>
          <dt className="text-xs text-[var(--muted)]">System prompt</dt>
          <dd className="whitespace-pre-wrap">{draft.systemPrompt}</dd>
        </div>
      </dl>
      {error ? <p className="mt-2 text-sm text-red-600">{error}</p> : null}
      <button
        type="button"
        className="mt-3 rounded bg-[var(--accent)] px-3 py-1.5 text-white"
        disabled={busy}
        onClick={() => {
          setBusy(true)
          setError(null)
          const body = toCreateAgentBody(draft)
          void createAgent(body)
            .then((agent) => onCreated(messageId, agent.id))
            .catch((err: ApiError) => {
              if (err.code === 'agent_name_taken') {
                setError(
                  'That name is already taken. Ask the builder for a new name.',
                )
              } else {
                setError(err.detail ?? err.title ?? 'Create failed')
              }
            })
            .finally(() => setBusy(false))
        }}
      >
        Create agent
      </button>
    </div>
  )
}
