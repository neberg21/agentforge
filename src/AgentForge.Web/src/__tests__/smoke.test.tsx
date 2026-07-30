import { afterEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'

afterEach(() => {
  vi.unstubAllGlobals()
  vi.resetModules()
})

describe('smoke', () => {
  it('renders AgentForge shell after areas load', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo) => {
        const url = String(input)
        if (url.includes('/api/areas')) {
          return new Response(JSON.stringify([{ slug: 'agents' }]), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          })
        }
        return new Response(JSON.stringify({ items: [], total: 0, skip: 0, take: 50 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        })
      }),
    )

    const { default: App } = await import('../App')
    render(<App />)
    await waitFor(() => {
      expect(screen.getByText('AgentForge')).toBeInTheDocument()
    })
  })
})
