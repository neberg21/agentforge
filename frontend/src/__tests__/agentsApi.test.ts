import { afterEach, describe, expect, it, vi } from 'vitest'
import { createConversation, listAgents, postConversationMessage, startRun } from '../areas/agents/api'

afterEach(() => {
  vi.unstubAllGlobals()
})

function lastFetchCall(fetchMock: ReturnType<typeof vi.fn>): [RequestInfo | URL, RequestInit | undefined] {
  const call = fetchMock.mock.calls.at(-1)
  if (!call) {
    throw new Error('fetch was not called')
  }
  return [call[0] as RequestInfo | URL, call[1] as RequestInit | undefined]
}

describe('agents api client', () => {
  it('listAgents sends q when provided', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ items: [], total: 0, skip: 0, take: 50 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await listAgents({ q: 'coder', skip: 0, take: 50 })
    const [url] = lastFetchCall(fetchMock)
    expect(String(url)).toBe('/api/agents/definitions?q=coder&skip=0&take=50')
  })

  it('startRun includes optional conversationId', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ id: 'run-1' }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' },
        }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await startRun({
      agentId: 'agent-1',
      objective: 'Ship it',
      conversationId: 'conv-1',
    })
    const [, init] = lastFetchCall(fetchMock)
    expect(init).toMatchObject({
      method: 'POST',
      body: JSON.stringify({
        agentId: 'agent-1',
        objective: 'Ship it',
        conversationId: 'conv-1',
      }),
    })
  })

  it('postConversationMessage posts mentions body', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ streamId: 's1' }), {
          status: 202,
          headers: { 'Content-Type': 'application/json' },
        }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await postConversationMessage('conv-1', {
      content: 'hello',
      mentions: ['agent-a'],
    })
    const [url, init] = lastFetchCall(fetchMock)
    expect(String(url)).toBe('/api/agents/conversations/conv-1/messages')
    expect(init).toMatchObject({
      method: 'POST',
      body: JSON.stringify({ content: 'hello', mentions: ['agent-a'] }),
    })
  })

  it('createConversation posts participants', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ id: 'c1', title: 'Chat' }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' },
        }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await createConversation({ participantAgentIds: ['a1', 'a2'] })
    const [, init] = lastFetchCall(fetchMock)
    expect(init).toMatchObject({
      method: 'POST',
      body: JSON.stringify({ participantAgentIds: ['a1', 'a2'] }),
    })
  })
})
