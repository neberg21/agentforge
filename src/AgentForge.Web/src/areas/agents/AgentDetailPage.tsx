import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { archiveAgent, getAgent } from './api'
import type { AgentDto } from './types'
import { rememberItem } from '../../lib/recent'
import { useContextPanel } from '../../shell/ContextPanel'

export function AgentDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { setContent } = useContextPanel()
  const [agent, setAgent] = useState<AgentDto | null>(null)

  useEffect(() => {
    if (!id) {
      return
    }
    void getAgent(id).then((value) => {
      setAgent(value)
      rememberItem({ kind: 'agent', id: value.id, label: value.name })
    })
  }, [id])

  useEffect(() => {
    if (!agent) {
      setContent(null)
      return
    }
    setContent(
      <div className="space-y-2 text-sm">
        <div className="font-semibold">Context</div>
        <div>Model: {agent.model}</div>
        <div>Turns: {agent.maxTurns}</div>
        <div>Tools: {agent.allowedTools.join(', ') || 'none'}</div>
      </div>,
    )
    return () => setContent(null)
  }, [agent, setContent])

  if (!agent) {
    return <p>Loading…</p>
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-2xl font-semibold">{agent.name}</h1>
        <span className="text-sm text-[var(--muted)]">{agent.model}</span>
      </div>
      <pre className="whitespace-pre-wrap rounded border border-[var(--border)] bg-[var(--panel)] p-4 text-sm">
        {agent.systemPrompt}
      </pre>
      <div className="flex flex-wrap gap-3 text-sm">
        <Link className="underline" to={`/agents/definitions/${agent.id}/edit`}>
          Edit
        </Link>
        <button
          type="button"
          className="underline"
          onClick={() => navigate(`/agents/runs?start=${agent.id}`)}
        >
          Start run
        </button>
        <button
          type="button"
          className="underline"
          onClick={() => navigate(`/agents/conversations?new=${agent.id}`)}
        >
          Start conversation
        </button>
        <button
          type="button"
          className="underline"
          onClick={() => {
            if (!window.confirm(`Archive “${agent.name}”?`)) {
              return
            }
            void archiveAgent(agent.id).then(() => navigate('/agents/definitions'))
          }}
        >
          Archive
        </button>
      </div>
    </div>
  )
}
