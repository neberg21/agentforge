import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MessageBody } from '../areas/agents/MessageBody'

const leo = { agentId: 'id-leo', name: 'leo' }

describe('MessageBody', () => {
  it('highlights @Name and omits To chip when text contains the mention', () => {
    render(
      <MessageBody
        role="User"
        content="@leo please review"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.getByText('@leo')).toBeInTheDocument()
    expect(screen.queryByText(/To:/)).toBeNull()
  })

  it('shows To chip when mention ids exist but @Name is absent', () => {
    render(
      <MessageBody
        role="User"
        content="please review"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.getByText('To: @leo')).toBeInTheDocument()
  })

  it('shows no To chips for assistant messages', () => {
    render(
      <MessageBody
        role="Assistant"
        content="ok"
        mentions={['id-leo']}
        participants={[leo]}
      />,
    )
    expect(screen.queryByText(/To:/)).toBeNull()
  })
})
