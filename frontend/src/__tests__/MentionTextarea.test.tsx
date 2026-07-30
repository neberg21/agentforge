import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MentionTextarea } from '../areas/agents/MentionTextarea'

const participants = [
  { agentId: 'id-leo', name: 'leo' },
  { agentId: 'id-max', name: 'max' },
]

function Harness({ onSubmit }: { onSubmit?: () => void }) {
  const [value, setValue] = useState('')
  return (
    <div>
      <MentionTextarea
        value={value}
        onChange={setValue}
        participants={participants}
        required
        onSubmit={onSubmit}
      />
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

  it('calls onSubmit on Enter without Shift when mention menu is closed', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hello{Enter}')
    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('does not call onSubmit on Shift+Enter', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, 'hello{Shift>}{Enter}{/Shift}')
    expect(onSubmit).not.toHaveBeenCalled()
    expect(screen.getByTestId('value').textContent).toContain('\n')
  })

  it('inserts mention on Enter when menu is open and does not call onSubmit', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<Harness onSubmit={onSubmit} />)

    const box = screen.getByRole('textbox')
    await user.type(box, '@l{Enter}')
    expect(screen.getByTestId('value').textContent).toMatch(/@leo /)
    expect(onSubmit).not.toHaveBeenCalled()
  })
})
