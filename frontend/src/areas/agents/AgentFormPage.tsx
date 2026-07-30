import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { createAgent, getAgent, getAgentSuggestions, updateAgent } from './api'
import type { ApiError } from '../../lib/http'

const empty = {
  name: '',
  description: '',
  systemPrompt: '',
  model: '',
  temperature: 0.7,
  maxOutputTokens: 4096,
  maxTurns: 20,
  allowedTools: 'read_file',
}

export function AgentFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const editing = Boolean(id)
  const [form, setForm] = useState(empty)
  const [token, setToken] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [fieldError, setFieldError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      return
    }
    void getAgent(id).then((agent) => {
      setForm({
        name: agent.name,
        description: agent.description ?? '',
        systemPrompt: agent.systemPrompt,
        model: agent.model,
        temperature: agent.temperature,
        maxOutputTokens: agent.maxOutputTokens,
        maxTurns: agent.maxTurns,
        allowedTools: agent.allowedTools.join(', '),
      })
      setToken(agent.concurrencyToken)
    })
  }, [id])

  useEffect(() => {
    if (editing) {
      return
    }
    let cancelled = false
    void getAgentSuggestions()
      .then((suggestions) => {
        if (cancelled) {
          return
        }
        setForm((current) => {
          if (current.name.trim() !== '') {
            return current
          }
          return { ...current, name: suggestions.name }
        })
      })
      .catch(() => {
        // leave name empty
      })
    return () => {
      cancelled = true
    }
  }, [editing])

  async function onSubmit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setFieldError(null)
    const tools = form.allowedTools
      .split(',')
      .map((part) => part.trim())
      .filter(Boolean)
    const body = {
      name: form.name,
      description: form.description || null,
      systemPrompt: form.systemPrompt,
      model: form.model,
      temperature: form.temperature,
      maxOutputTokens: form.maxOutputTokens,
      maxTurns: form.maxTurns,
      allowedTools: tools,
      concurrencyToken: token,
    }
    try {
      const saved = editing && id ? await updateAgent(id, body) : await createAgent(body)
      navigate(`/agents/definitions/${saved.id}`)
    } catch (err) {
      const apiError = err as ApiError
      if (apiError.code === 'agent_name_taken') {
        setFieldError('Name is already taken.')
      } else if (apiError.code === 'concurrency_conflict') {
        setError('Changed elsewhere. Reload and try again.')
      } else {
        setError(apiError.detail ?? apiError.title)
      }
    }
  }

  return (
    <form className="mx-auto max-w-2xl space-y-4" onSubmit={(event) => void onSubmit(event)}>
      <h1 className="text-2xl font-semibold">{editing ? 'Edit agent' : 'New agent'}</h1>
      {error ? <p className="text-sm text-red-600">{error}</p> : null}
      <label className="block text-sm">
        Name
        <input
          className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={form.name}
          onChange={(event) => setForm({ ...form, name: event.target.value })}
          required
          maxLength={100}
        />
        {fieldError ? <span className="text-red-600">{fieldError}</span> : null}
      </label>
      <label className="block text-sm">
        Description
        <input
          className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={form.description}
          onChange={(event) => setForm({ ...form, description: event.target.value })}
        />
      </label>
      <label className="block text-sm">
        System prompt
        <textarea
          className="mt-1 min-h-40 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={form.systemPrompt}
          onChange={(event) => setForm({ ...form, systemPrompt: event.target.value })}
          required
        />
      </label>
      <label className="block text-sm">
        Model
        <input
          className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={form.model}
          onChange={(event) => setForm({ ...form, model: event.target.value })}
          required
        />
      </label>
      <div className="grid grid-cols-3 gap-3">
        <label className="block text-sm">
          Temperature
          <input
            type="number"
            step="0.1"
            min={0}
            max={2}
            className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
            value={form.temperature}
            onChange={(event) => setForm({ ...form, temperature: Number(event.target.value) })}
          />
        </label>
        <label className="block text-sm">
          Max output tokens
          <input
            type="number"
            min={1}
            max={200000}
            className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
            value={form.maxOutputTokens}
            onChange={(event) => setForm({ ...form, maxOutputTokens: Number(event.target.value) })}
          />
        </label>
        <label className="block text-sm">
          Max turns
          <input
            type="number"
            min={1}
            max={200}
            className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
            value={form.maxTurns}
            onChange={(event) => setForm({ ...form, maxTurns: Number(event.target.value) })}
          />
        </label>
      </div>
      <label className="block text-sm">
        Tools (comma-separated)
        <input
          className="mt-1 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={form.allowedTools}
          onChange={(event) => setForm({ ...form, allowedTools: event.target.value })}
        />
      </label>
      <div className="flex gap-3">
        <button type="submit" className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white">
          Save
        </button>
        <Link to="/agents/definitions" className="px-3 py-1.5 text-sm underline">
          Cancel
        </Link>
      </div>
    </form>
  )
}
