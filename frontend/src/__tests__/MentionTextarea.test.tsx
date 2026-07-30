import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { MentionTextarea } from '../areas/agents/MentionTextarea'

const participants = [
  { agentId: 'id-leo', name: 'leo' },
  { agentId: 'id-max', name: 'max' },
]

function Harness() {
  const [value, setValue] = useState('')
  return (
    <div>
      <MentionTextarea value={value} onChange={setValue} participants={participants} required />
      <output data-testid="value">{value}</output>
    </div>
  )
}

describe('MentionTextarea', () => {
  it('opens participant menu after typing @ and inserts on click', async () => {
    const user = userEvent.setup()
    render(<Harness />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hi @l')
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'leo' })).toBeInTheDocument()

    await user.click(screen.getByRole('option', { name: 'leo' }))
    expect(screen.getByTestId('value').textContent).toMatch(/@leo /)
  })
})
