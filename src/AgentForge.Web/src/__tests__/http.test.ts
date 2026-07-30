import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiGet } from '../lib/http'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('apiGet', () => {
  it('maps problem details code extension', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              title: 'Conflict',
              detail: 'taken',
              code: 'agent_name_taken',
            }),
            { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
          ),
      ),
    )
    await expect(apiGet('/api/agents/definitions')).rejects.toMatchObject({
      status: 409,
      code: 'agent_name_taken',
    })
  })

  it('omits empty query values', async () => {
    const fetchMock = vi.fn(
      async () =>
        new Response(JSON.stringify({ items: [], total: 0, skip: 0, take: 50 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
    )
    vi.stubGlobal('fetch', fetchMock)
    await apiGet('/api/agents/definitions', { q: '', skip: 0, take: 50 })
    expect(fetchMock).toHaveBeenCalledWith('/api/agents/definitions?skip=0&take=50')
  })
})
