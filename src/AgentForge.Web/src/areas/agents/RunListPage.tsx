import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { listAgents, listRuns, startRun } from './api'
import type { AgentDto, RunDto } from './types'
import type { ApiError } from '../../lib/http'
import { rememberItem } from '../../lib/recent'

export function RunListPage() {
  const [runs, setRuns] = useState<RunDto[]>([])
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [objective, setObjective] = useState('')
  const [agentId, setAgentId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const startPrefill = params.get('start')

  useEffect(() => {
    void listRuns({ skip: 0, take: 50 }).then((page) => setRuns(page.items))
    void listAgents({ skip: 0, take: 200 }).then((page) => {
      setAgents(page.items)
      if (startPrefill) {
        setAgentId(startPrefill)
      } else if (page.items[0]) {
        setAgentId(page.items[0].id)
      }
    })
  }, [startPrefill])

  const agentNames = useMemo(
    () => Object.fromEntries(agents.map((agent) => [agent.id, agent.name])),
    [agents],
  )

  async function onStart(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    try {
      const run = await startRun({ agentId, objective })
      rememberItem({ kind: 'run', id: run.id, label: run.objective.slice(0, 40) })
      navigate(`/agents/runs/${run.id}`)
    } catch (err) {
      const apiError = err as ApiError
      setError(
        apiError.code === 'agent_archived'
          ? 'This agent is archived and cannot run.'
          : (apiError.detail ?? apiError.title),
      )
    }
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Runs</h1>
      <form className="space-y-3 rounded border border-[var(--border)] bg-[var(--panel)] p-4" onSubmit={(e) => void onStart(e)}>
        <div className="font-medium">Start run</div>
        <label className="block text-sm">
          Agent
          <select
            className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
            value={agentId}
            onChange={(event) => setAgentId(event.target.value)}
          >
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name}
              </option>
            ))}
          </select>
        </label>
        <label className="block text-sm">
          Objective
          <textarea
            className="mt-1 min-h-24 w-full rounded border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
            value={objective}
            onChange={(event) => setObjective(event.target.value)}
            required
          />
        </label>
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        <button type="submit" className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white">
          Start
        </button>
      </form>
      <ul className="space-y-2">
        {runs.map((run) => (
          <li key={run.id} className="rounded border border-[var(--border)] bg-[var(--panel)] p-3">
            <Link
              className="font-medium hover:underline"
              to={`/agents/runs/${run.id}`}
              onClick={() =>
                rememberItem({ kind: 'run', id: run.id, label: run.objective.slice(0, 40) })
              }
            >
              {run.objective}
            </Link>
            <div className="text-sm text-[var(--muted)]">
              {agentNames[run.agentId] ?? run.agentId} · {run.status}
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}
