import { MemoryRouter, Route, Routes } from 'react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AppShell } from '../shell/AppShell'
import { ContextPanelProvider } from '../shell/ContextPanel'

const nav = [
  { to: '/agents/definitions', label: 'Agents' },
  { to: '/agents/runs', label: 'Runs' },
  { to: '/agents/conversations', label: 'Conversations' },
]

function renderShell() {
  return render(
    <MemoryRouter initialEntries={['/agents/definitions']}>
      <ContextPanelProvider>
        <Routes>
          <Route path="/agents" element={<AppShell areaTitle="Agents" nav={nav} />}>
            <Route path="definitions" element={<div>Definitions page</div>} />
            <Route path="runs" element={<div>Runs page</div>} />
            <Route path="conversations" element={<div>Conversations page</div>} />
          </Route>
        </Routes>
      </ContextPanelProvider>
    </MemoryRouter>,
  )
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('AppShell mobile nav', () => {
  it('opens the drawer from the burger and closes on backdrop click', async () => {
    const user = userEvent.setup()
    renderShell()

    expect(screen.queryByRole('dialog', { name: /navigation/i })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /open menu/i }))
    expect(screen.getByRole('dialog', { name: /navigation/i })).toBeInTheDocument()
    expect(screen.getByRole('dialog').textContent).toMatch(/Recent/)

    await user.click(screen.getByTestId('nav-backdrop'))
    expect(screen.queryByRole('dialog', { name: /navigation/i })).not.toBeInTheDocument()
  })

  it('closes the drawer on Escape', async () => {
    const user = userEvent.setup()
    renderShell()

    await user.click(screen.getByRole('button', { name: /open menu/i }))
    expect(screen.getByRole('dialog', { name: /navigation/i })).toBeInTheDocument()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: /navigation/i })).not.toBeInTheDocument()
  })

  it('closes the drawer when a nav link is chosen', async () => {
    const user = userEvent.setup()
    renderShell()

    await user.click(screen.getByRole('button', { name: /open menu/i }))
    const dialog = screen.getByRole('dialog', { name: /navigation/i })
    await user.click(dialog.querySelector('a[href="/agents/runs"]')!)

    expect(screen.queryByRole('dialog', { name: /navigation/i })).not.toBeInTheDocument()
    expect(screen.getByText('Runs page')).toBeInTheDocument()
  })
})
