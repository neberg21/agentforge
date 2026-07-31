import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

vi.mock('../areas/agents/api', () => ({
  patchConversationTitle: vi.fn(),
}))

import { patchConversationTitle } from '../areas/agents/api'
import { ConversationTitleHeader } from '../areas/agents/ConversationTitleHeader'

describe('ConversationTitleHeader', () => {
  it('shows OK when auto and calls lock', async () => {
    const user = userEvent.setup()
    const onUpdated = vi.fn()
    vi.mocked(patchConversationTitle).mockResolvedValue({
      id: 'c1',
      title: 'New conversation',
      titleMode: 'locked',
      participants: [],
      lastMessageExcerpt: null,
      lastMessageAt: null,
      createdAt: '',
      updatedAt: '',
      archivedAt: null,
      concurrencyToken: 't2',
    })

    render(
      <ConversationTitleHeader
        conversationId="c1"
        title="New conversation"
        titleMode="auto"
        concurrencyToken="t1"
        onUpdated={onUpdated}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'OK' }))
    expect(patchConversationTitle).toHaveBeenCalledWith('c1', {
      action: 'lock',
      concurrencyToken: 't1',
    })
    await waitFor(() => expect(onUpdated).toHaveBeenCalled())
  })

  it('shows Auto when locked', () => {
    render(
      <ConversationTitleHeader
        conversationId="c1"
        title="Named"
        titleMode="locked"
        concurrencyToken="t1"
        onUpdated={() => undefined}
      />,
    )
    expect(screen.getByRole('button', { name: 'Auto' })).toBeInTheDocument()
  })
})
