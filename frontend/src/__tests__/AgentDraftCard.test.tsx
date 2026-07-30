import { describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { AgentDraftCard } from '../areas/agents/AgentDraftCard'
import type { ValidAgentDraft } from '../areas/agents/agentDraft'

const draft: ValidAgentDraft = {
  name: 'Coder',
  description: 'Writes code',
  systemPrompt: 'You write code.',
  model: null,
  temperature: null,
  maxOutputTokens: null,
  maxTurns: null,
  allowedTools: null,
}

vi.mock('../areas/agents/api', () => ({
  createAgent: vi.fn(async () => ({ id: 'new-1', name: 'Coder' })),
}))

describe('AgentDraftCard', () => {
  it('creates agent and shows link', async () => {
    const onCreated = vi.fn()
    const { rerender } = render(
      <MemoryRouter>
        <AgentDraftCard
          messageId="m1"
          draft={draft}
          createdAgentId={null}
          onCreated={onCreated}
        />
      </MemoryRouter>,
    )
    fireEvent.click(screen.getByRole('button', { name: /create agent/i }))
    await waitFor(() => expect(onCreated).toHaveBeenCalledWith('m1', 'new-1'))
    rerender(
      <MemoryRouter>
        <AgentDraftCard
          messageId="m1"
          draft={draft}
          createdAgentId="new-1"
          onCreated={onCreated}
        />
      </MemoryRouter>,
    )
    expect(screen.getByRole('link', { name: /open agent/i })).toHaveAttribute(
      'href',
      '/agents/definitions/new-1',
    )
  })
})
