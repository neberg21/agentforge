import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { archiveAgent, listAgents, startBuilderSession } from './api'
import type { AgentDto } from './types'
import { rememberItem } from '../../lib/recent'
import type { ApiError } from '../../lib/http'

export function AgentListPage() {
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const [debounced, setDebounced] = useState('')
  const [items, setItems] = useState<AgentDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [startingBuilder, setStartingBuilder] = useState(false)

  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(q), 300)
    return () => window.clearTimeout(handle)
  }, [q])

  useEffect(() => {
    let cancelled = false
    void listAgents({ q: debounced, skip: 0, take: 50 })
      .then((page) => {
        if (!cancelled) {
          setItems(page.items)
          setError(null)
        }
      })
      .catch((err: ApiError) => {
        if (!cancelled) {
          setError(err.detail ?? err.title)
        }
      })
    return () => {
      cancelled = true
    }
  }, [debounced])

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Agents</h1>
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="rounded border border-[var(--border)] px-3 py-1.5 text-sm"
            disabled={startingBuilder}
            onClick={() => {
              setStartingBuilder(true)
              void startBuilderSession()
                .then((session) => {
                  navigate(`/agents/conversations/${session.conversationId}`)
                })
                .catch((err: ApiError) => {
                  setError(err.detail ?? err.title)
                })
                .finally(() => setStartingBuilder(false))
            }}
          >
            Create with assistant
          </button>
          <Link
            to="/agents/definitions/new"
            className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white"
          >
            New agent
          </Link>
        </div>
      </div>
      <input
        className="mb-4 w-full max-w-md rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
        placeholder="Search by name"
        value={q}
        onChange={(event) => setQ(event.target.value)}
        aria-label="Search agents"
      />
      {error ? <p className="mb-3 text-sm text-red-600">{error}</p> : null}
      {items.length === 0 ? (
        <p className="text-[var(--muted)]">
          No agents yet.{' '}
          <Link className="underline" to="/agents/definitions/new">
            Create an agent
          </Link>
        </p>
      ) : (
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-[var(--border)] text-[var(--muted)]">
              <th className="py-2">Name</th>
              <th>Model</th>
              <th>Updated</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.map((agent) => (
              <tr key={agent.id} className="border-b border-[var(--border)]">
                <td className="py-2">
                  <Link
                    className="font-medium underline-offset-2 hover:underline"
                    to={`/agents/definitions/${agent.id}`}
                    onClick={() =>
                      rememberItem({ kind: 'agent', id: agent.id, label: agent.name })
                    }
                  >
                    {agent.name}
                  </Link>
                </td>
                <td>{agent.model}</td>
                <td>{new Date(agent.updatedAt).toLocaleString()}</td>
                <td className="space-x-2 text-right">
                  <button
                    type="button"
                    className="underline"
                    onClick={() => navigate(`/agents/conversations?agentId=${agent.id}`)}
                  >
                    Chat
                  </button>
                  <button
                    type="button"
                    className="underline"
                    onClick={() => navigate(`/agents/runs?start=${agent.id}`)}
                  >
                    Run
                  </button>
                  <button
                    type="button"
                    aria-label={`Archive ${agent.name}`}
                    className="underline"
                    onClick={() => {
                      if (!window.confirm(`Archive “${agent.name}”?`)) {
                        return
                      }
                      void archiveAgent(agent.id).then(() =>
                        setItems((current) => current.filter((item) => item.id !== agent.id)),
                      )
                    }}
                  >
                    Archive
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
