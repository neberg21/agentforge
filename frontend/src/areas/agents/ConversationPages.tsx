import { useEffect, useReducer, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import {
  createConversation,
  draftRun,
  getConversation,
  getConversationMessages,
  listAgents,
  listConversations,
  postConversationMessage,
  startRun,
} from './api'
import type { AgentDto, ConversationDto, ConversationMessageDto } from './types'
import { openEventSource } from '../../lib/sse'
import {
  emptyTranscript,
  messagesInOrder,
  transcriptReducer,
  type TranscriptMessage,
} from './transcriptReducer'
import { rememberItem } from '../../lib/recent'
import { useContextPanel } from '../../shell/ContextPanel'
import { ToolCallCard } from './ToolCallCard'
import { MentionTextarea } from './MentionTextarea'
import { MessageBody } from './MessageBody'
import { AgentDraftCard } from './AgentDraftCard'
import { parseAgentDraft, stripAgentDraftFence } from './agentDraft'
import { autoMentionPosition } from './mentionConfig'
import { prepareOutgoingMessage } from './mentions'
import type { ApiError } from '../../lib/http'

function senderColor(agentId: string): string {
  let hash = 0
  for (let i = 0; i < agentId.length; i += 1) {
    hash = (hash * 31 + agentId.charCodeAt(i)) >>> 0
  }
  const hue = hash % 360
  return `hsl(${hue} 45% 40%)`
}

function toTranscript(messages: ConversationMessageDto[]): TranscriptMessage[] {
  return messages.map((message) => ({
    id: message.id,
    sequence: message.sequence,
    role: message.role,
    content: message.content,
    toolCallsJson: message.toolCallsJson,
    toolCallId: message.toolCallId,
    senderAgentId: message.senderAgentId,
    senderName: message.senderName,
    mentions: message.mentions,
  }))
}

export function ConversationListPage() {
  const [items, setItems] = useState<ConversationDto[]>([])
  const [agents, setAgents] = useState<AgentDto[]>([])
  const [selected, setSelected] = useState<string[]>([])
  const [title, setTitle] = useState('')
  const [params] = useSearchParams()
  const navigate = useNavigate()

  useEffect(() => {
    void listConversations({ skip: 0, take: 50 }).then((page) => setItems(page.items))
    void listAgents({ skip: 0, take: 200 }).then((page) => {
      setAgents(page.items)
      const pref = params.get('new') ?? params.get('agentId')
      if (pref) {
        setSelected([pref])
      }
    })
  }, [params])

  async function onCreate(event: React.FormEvent) {
    event.preventDefault()
    const created = await createConversation({
      title: title || undefined,
      participantAgentIds: selected,
    })
    rememberItem({ kind: 'conversation', id: created.id, label: created.title })
    navigate(`/agents/conversations/${created.id}`)
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold">Conversations</h1>
      <form className="space-y-3 rounded border border-[var(--border)] bg-[var(--panel)] p-4" onSubmit={(e) => void onCreate(e)}>
        <div className="font-medium">New conversation</div>
        <input
          className="w-full rounded border border-[var(--border)] bg-[var(--bg)] px-3 py-2"
          placeholder="Title (optional)"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
        />
        <div className="flex flex-wrap gap-2">
          {agents.map((agent) => {
            const checked = selected.includes(agent.id)
            return (
              <label key={agent.id} className="flex items-center gap-1 text-sm">
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={() =>
                    setSelected((current) =>
                      checked ? current.filter((id) => id !== agent.id) : [...current, agent.id],
                    )
                  }
                />
                {agent.name}
              </label>
            )
          })}
        </div>
        <button
          type="submit"
          disabled={selected.length === 0}
          className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white disabled:opacity-50"
        >
          Create
        </button>
      </form>
      <ul className="space-y-2">
        {items.map((item) => (
          <li key={item.id} className="rounded border border-[var(--border)] bg-[var(--panel)] p-3">
            <Link
              className="font-medium hover:underline"
              to={`/agents/conversations/${item.id}`}
              onClick={() =>
                rememberItem({ kind: 'conversation', id: item.id, label: item.title })
              }
            >
              {item.title}
            </Link>
            <div className="text-sm text-[var(--muted)]">
              {item.participants.map((participant) => participant.name).join(', ')}
            </div>
          </li>
        ))}
      </ul>
    </div>
  )
}

export function ConversationPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { setContent } = useContextPanel()
  const [conversation, setConversation] = useState<ConversationDto | null>(null)
  const [content, setMessage] = useState('')
  const [hint, setHint] = useState<string | null>(null)
  const [draftOpen, setDraftOpen] = useState(false)
  const [draftObjective, setDraftObjective] = useState('')
  const [draftAgentId, setDraftAgentId] = useState('')
  const [createdDrafts, setCreatedDrafts] = useState<Record<string, string>>({})
  const [state, dispatch] = useReducer(transcriptReducer, undefined, emptyTranscript)

  useEffect(() => {
    if (!id) {
      return
    }
    let stop: (() => void) | undefined
    void Promise.all([getConversation(id), getConversationMessages(id)]).then(
      ([conversationValue, messages]) => {
        setConversation(conversationValue)
        rememberItem({
          kind: 'conversation',
          id: conversationValue.id,
          label: conversationValue.title,
        })
        dispatch({
          type: 'hydrate',
          messages: toTranscript(messages),
        })
        stop = openEventSource(`/api/agents/conversations/${id}/stream`, {
          onEvent: (type, data) => dispatch({ type: 'sse', event: type, data }),
          onError: () => undefined,
        })
      },
    )
    return () => stop?.()
  }, [id])

  useEffect(() => {
    if (!state.needsMessageReload || !id) {
      return
    }
    void getConversationMessages(id).then((messages) => {
      dispatch({
        type: 'reloadMessages',
        messages: toTranscript(messages),
      })
    })
  }, [state.needsMessageReload, id])

  useEffect(() => {
    if (!conversation) {
      setContent(null)
      return
    }
    setContent(
      <div className="space-y-3 text-sm">
        <div className="font-semibold">Participants</div>
        {conversation.participants.map((participant) => (
          <div key={participant.agentId} style={{ color: senderColor(participant.agentId) }}>
            <Link className="underline" to={`/agents/definitions/${participant.agentId}`}>
              {participant.name}
            </Link>
          </div>
        ))}
        <button
          type="button"
          className="rounded bg-[var(--accent)] px-3 py-1.5 text-white"
          onClick={() => {
            void draftRun(conversation.id).then((proposal) => {
              setDraftObjective(proposal.objective)
              setDraftAgentId(proposal.agentId)
              setDraftOpen(true)
            })
          }}
        >
          Draft run
        </button>
      </div>,
    )
    return () => setContent(null)
  }, [conversation, setContent])

  async function onSend(event: React.FormEvent) {
    event.preventDefault()
    if (!id || !conversation) {
      return
    }
    const prepared = prepareOutgoingMessage(
      content,
      conversation.participants,
      autoMentionPosition,
    )
    setHint(
      conversation.participants.length > 1 && prepared.mentions.length === 0
        ? 'Not addressed — no agent will reply.'
        : null,
    )
    try {
      await postConversationMessage(id, {
        content: prepared.content,
        mentions: prepared.mentions,
      })
      setMessage('')
      const messages = await getConversationMessages(id)
      dispatch({
        type: 'reloadMessages',
        messages: toTranscript(messages),
      })
    } catch (error) {
      const apiError = error as ApiError
      setHint(apiError.detail ?? apiError.title ?? 'Send failed')
    }
  }

  if (!conversation) {
    return <p>Loading…</p>
  }

  return (
    <div className="flex h-full flex-col gap-4">
      <h1 className="text-xl font-semibold">{conversation.title}</h1>
      <div className="flex-1 space-y-3 overflow-auto" role="log" aria-live="polite">
        {messagesInOrder(state)
          .filter((message) => message.role !== 'System')
          .map((message) => {
          const rawContent = message.content ?? ''
          const parsed =
            message.role === 'Assistant'
              ? parseAgentDraft(rawContent)
              : ({ ok: false, reason: 'missing' } as const)
          const displayContent =
            message.role === 'Assistant' ? stripAgentDraftFence(rawContent) : message.content

          return (
            <div
              key={message.id}
              className="rounded border border-[var(--border)] bg-[var(--panel)] p-3 text-sm"
            >
              <div
                className="mb-1 text-xs font-medium"
                style={{
                  color: message.senderAgentId ? senderColor(message.senderAgentId) : undefined,
                }}
              >
                {message.senderName ?? message.role}
              </div>
              <MessageBody
                role={message.role}
                content={displayContent}
                mentions={message.mentions}
                participants={conversation.participants}
              />
              {message.role === 'Assistant' && parsed.ok ? (
                <AgentDraftCard
                  messageId={message.id}
                  draft={parsed.draft}
                  createdAgentId={createdDrafts[message.id] ?? null}
                  onCreated={(msgId, agentId) =>
                    setCreatedDrafts((prev) => ({ ...prev, [msgId]: agentId }))
                  }
                />
              ) : null}
              {message.role === 'Assistant' && !parsed.ok && parsed.reason === 'invalid' ? (
                <p className="mt-2 text-sm text-red-600">
                  Draft incomplete — ask the builder to propose again.
                </p>
              ) : null}
              <ToolCallCard toolCallsJson={message.toolCallsJson} />
            </div>
          )
        })}
      </div>
      {hint ? <p className="text-sm text-[var(--muted)]">{hint}</p> : null}
      <form className="space-y-2 border-t border-[var(--border)] pt-3" onSubmit={(e) => void onSend(e)}>
        <MentionTextarea
          className="min-h-24 w-full rounded border border-[var(--border)] bg-[var(--panel)] px-3 py-2"
          value={content}
          onChange={setMessage}
          participants={conversation.participants}
          required
        />
        <button type="submit" className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white">
          Send
        </button>
      </form>
      {draftOpen ? (
        <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/40 p-4">
          <form
            className="w-full max-w-lg space-y-3 rounded bg-[var(--panel)] p-4"
            onSubmit={(event) => {
              event.preventDefault()
              void startRun({
                agentId: draftAgentId,
                objective: draftObjective,
                conversationId: conversation.id,
              }).then((run) => navigate(`/agents/runs/${run.id}`))
            }}
          >
            <h2 className="text-lg font-semibold">Draft run</h2>
            <label className="block text-sm">
              Agent
              <select
                className="mt-1 w-full rounded border border-[var(--border)] px-3 py-2"
                value={draftAgentId}
                onChange={(event) => setDraftAgentId(event.target.value)}
              >
                {conversation.participants.map((participant) => (
                  <option key={participant.agentId} value={participant.agentId}>
                    {participant.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block text-sm">
              Objective
              <textarea
                className="mt-1 min-h-32 w-full rounded border border-[var(--border)] px-3 py-2"
                value={draftObjective}
                onChange={(event) => setDraftObjective(event.target.value)}
                required
              />
            </label>
            <div className="flex gap-2">
              <button type="submit" className="rounded bg-[var(--accent)] px-3 py-1.5 text-sm text-white">
                Start run
              </button>
              <button type="button" className="underline" onClick={() => setDraftOpen(false)}>
                Cancel
              </button>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  )
}
