# Mobile Burger Nav Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On viewports below `md`, hide the permanent left sidebar and expose the full nav (brand, area links, Recent) via a burger overlay drawer.

**Architecture:** Extract sidebar body into `ShellSideNav`. `AppShell` keeps local `menuOpen` state: mobile top bar + overlay drawer below `md`; permanent `aside` at `md+`. Close on backdrop, Escape, or nav link click; clear open state when crossing to `md+`.

**Tech Stack:** React 19, TypeScript, Vite, Vitest, Testing Library, Tailwind CSS v4, React Router 7.

**Spec:** `docs/superpowers/specs/2026-07-30-mobile-burger-nav-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- Frontend only under `frontend/` — no backend changes.
- Windows: no `.ps1`/`.sh`; commits via `git commit -F` message file; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- UI copy: English (“Open menu” / “Close menu”).
- Do not change the right context panel `lg` breakpoint.

## File Structure

**Create**
- `frontend/src/shell/ShellSideNav.tsx` — brand, area title, primary nav, Recent (shared body)
- `frontend/src/__tests__/AppShell.nav.test.tsx` — burger open/close / Escape / backdrop / link close

**Modify**
- `frontend/src/shell/AppShell.tsx` — responsive shell: top bar, drawer, permanent aside

---

### Task 1: Extract `ShellSideNav`

**Files:**
- Create: `frontend/src/shell/ShellSideNav.tsx`
- Modify: `frontend/src/shell/AppShell.tsx` (use `ShellSideNav` inside the existing aside; behavior unchanged for now)

**Interfaces:**
- Consumes: `areaTitle: string`, `nav: { to: string; label: string }[]`, optional `onNavigate?: () => void`
- Produces: `ShellSideNav` component that renders AgentForge brand, area title, `NavLink`s, Recent list; calls `onNavigate` when any nav/Recent link is clicked

- [ ] **Step 1: Create `ShellSideNav.tsx`**

Create `frontend/src/shell/ShellSideNav.tsx`:

```tsx
import { NavLink } from 'react-router'
import { listRecent } from '../lib/recent'

export type ShellNavItem = { to: string; label: string }

type Props = {
  areaTitle: string
  nav: ShellNavItem[]
  onNavigate?: () => void
}

function recentTo(kind: string, id: string): string {
  if (kind === 'agent') {
    return `/agents/definitions/${id}`
  }
  if (kind === 'run') {
    return `/agents/runs/${id}`
  }
  return `/agents/conversations/${id}`
}

export function ShellSideNav({ areaTitle, nav, onNavigate }: Props) {
  const recent = listRecent()

  return (
    <>
      <div className="mb-6 text-lg font-semibold tracking-tight">AgentForge</div>
      <div className="mb-2 text-xs uppercase tracking-wide text-[var(--muted)]">{areaTitle}</div>
      <nav className="flex flex-col gap-1">
        {nav.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            onClick={onNavigate}
            className={({ isActive }) =>
              `rounded px-2 py-1.5 text-sm ${isActive ? 'bg-[var(--accent)]/15 text-[var(--accent)]' : 'hover:bg-black/5'}`
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
      <div className="mt-8 text-xs uppercase tracking-wide text-[var(--muted)]">Recent</div>
      <ul className="mt-2 space-y-1 text-sm">
        {recent.map((item) => (
          <li key={`${item.kind}-${item.id}`}>
            <NavLink
              to={recentTo(item.kind, item.id)}
              onClick={onNavigate}
              className="block truncate hover:underline"
            >
              {item.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </>
  )
}
```

- [ ] **Step 2: Wire `AppShell` to use `ShellSideNav` (desktop-only behavior still)**

Replace the inline sidebar body in `frontend/src/shell/AppShell.tsx` so the left aside becomes:

```tsx
import { Outlet } from 'react-router'
import { useContextPanel } from './ContextPanel'
import { ShellSideNav } from './ShellSideNav'

export function AppShell({
  areaTitle,
  nav,
}: {
  areaTitle: string
  nav: { to: string; label: string }[]
}) {
  const { content } = useContextPanel()

  return (
    <div className="flex h-full min-h-0">
      <aside className="flex w-64 shrink-0 flex-col border-r border-[var(--border)] bg-[var(--panel)] p-4">
        <ShellSideNav areaTitle={areaTitle} nav={nav} />
      </aside>
      <main className="min-w-0 flex-1 overflow-auto p-6">
        <Outlet />
      </main>
      <aside className="hidden w-72 shrink-0 overflow-auto border-l border-[var(--border)] bg-[var(--panel)] p-4 lg:block">
        {content}
      </aside>
    </div>
  )
}
```

Remove unused `NavLink` / `listRecent` imports from `AppShell` (they move to `ShellSideNav`).

- [ ] **Step 3: Verify smoke still passes**

Run: `cmd /c "cd frontend && npx vitest run src/__tests__/smoke.test.tsx"`

Expected: PASS — still finds “AgentForge”.

- [ ] **Step 4: Commit**

```cmd
git add frontend/src/shell/ShellSideNav.tsx frontend/src/shell/AppShell.tsx
(
echo refactor: extract ShellSideNav for shared shell menu body
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 2: Burger top bar + overlay drawer below `md`

**Files:**
- Modify: `frontend/src/shell/AppShell.tsx`
- Create: `frontend/src/__tests__/AppShell.nav.test.tsx`

**Interfaces:**
- Consumes: `ShellSideNav` from Task 1 (`onNavigate` closes drawer)
- Produces: Mobile top bar (`md:hidden`) with burger; overlay drawer when `menuOpen`; permanent aside `hidden md:flex`; Escape / backdrop / link close; resize to `md+` clears open state

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/__tests__/AppShell.nav.test.tsx`:

```tsx
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
          <Route
            path="/agents"
            element={<AppShell areaTitle="Agents" nav={nav} />}
          >
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cmd /c "cd frontend && npx vitest run src/__tests__/AppShell.nav.test.tsx"`

Expected: FAIL — no “Open menu” button / no dialog.

- [ ] **Step 3: Implement responsive `AppShell`**

Replace `frontend/src/shell/AppShell.tsx` with:

```tsx
import { useEffect, useState } from 'react'
import { Outlet } from 'react-router'
import { useContextPanel } from './ContextPanel'
import { ShellSideNav } from './ShellSideNav'

const MD_MIN_WIDTH_PX = 768

export function AppShell({
  areaTitle,
  nav,
}: {
  areaTitle: string
  nav: { to: string; label: string }[]
}) {
  const { content } = useContextPanel()
  const [menuOpen, setMenuOpen] = useState(false)

  useEffect(() => {
    if (!menuOpen) {
      return
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMenuOpen(false)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [menuOpen])

  useEffect(() => {
    function onResize() {
      if (window.innerWidth >= MD_MIN_WIDTH_PX) {
        setMenuOpen(false)
      }
    }
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  function closeMenu() {
    setMenuOpen(false)
  }

  return (
    <div className="flex h-full min-h-0 flex-col md:flex-row">
      <header className="flex shrink-0 items-center gap-3 border-b border-[var(--border)] bg-[var(--panel)] px-3 py-2 md:hidden">
        <button
          type="button"
          className="rounded px-2 py-1 text-lg leading-none"
          aria-expanded={menuOpen}
          aria-controls="app-shell-nav-drawer"
          aria-label={menuOpen ? 'Close menu' : 'Open menu'}
          onClick={() => setMenuOpen((open) => !open)}
        >
          ☰
        </button>
        <div className="text-lg font-semibold tracking-tight">AgentForge</div>
      </header>

      <div className="flex min-h-0 min-w-0 flex-1">
        <aside className="hidden w-64 shrink-0 flex-col border-r border-[var(--border)] bg-[var(--panel)] p-4 md:flex">
          <ShellSideNav areaTitle={areaTitle} nav={nav} />
        </aside>

        <main className="min-w-0 flex-1 overflow-auto p-6">
          <Outlet />
        </main>

        <aside className="hidden w-72 shrink-0 overflow-auto border-l border-[var(--border)] bg-[var(--panel)] p-4 lg:block">
          {content}
        </aside>
      </div>

      {menuOpen ? (
        <div className="fixed inset-0 z-40 md:hidden">
          <button
            type="button"
            data-testid="nav-backdrop"
            aria-label="Close menu"
            className="absolute inset-0 bg-black/40"
            onClick={closeMenu}
          />
          <div
            id="app-shell-nav-drawer"
            role="dialog"
            aria-modal="true"
            aria-label="Navigation"
            className="absolute inset-y-0 left-0 flex w-64 flex-col border-r border-[var(--border)] bg-[var(--panel)] p-4 shadow-lg"
          >
            <ShellSideNav areaTitle={areaTitle} nav={nav} onNavigate={closeMenu} />
          </div>
        </div>
      ) : null}
    </div>
  )
}
```

Notes:
- Burger glyph `☰` is fine; accessible name comes from `aria-label`.
- Drawer mounts only when `menuOpen` so `role="dialog"` is absent when closed.
- `md:hidden` on the overlay keeps it from showing if open state somehow remains on wide viewports; resize effect also clears state.

- [ ] **Step 4: Run nav tests to verify they pass**

Run: `cmd /c "cd frontend && npx vitest run src/__tests__/AppShell.nav.test.tsx"`

Expected: PASS (all three cases).

- [ ] **Step 5: Run smoke + lint**

Run: `cmd /c "cd frontend && npx vitest run src/__tests__/smoke.test.tsx src/__tests__/AppShell.nav.test.tsx && npm run lint"`

Expected: PASS / no type errors.

- [ ] **Step 6: Commit**

```cmd
git add frontend/src/shell/AppShell.tsx frontend/src/__tests__/AppShell.nav.test.tsx
(
echo feat: collapse shell nav into burger drawer below md
) > %TEMP%\commitmsg.txt
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

## Manual verification

1. Narrow viewport (&lt; 768px): no permanent left column; top bar with burger + AgentForge; main full width.
2. Open burger: drawer shows brand, area title, Agents/Runs/Conversations, Recent; backdrop dims the rest.
3. Backdrop / Escape / nav link closes drawer.
4. Wide viewport (≥ 768px): permanent left aside; no top bar; context panel still only at `lg+`.

---

## Spec coverage checklist

| Spec requirement | Task |
|---|---|
| Shared full sidebar body (brand, nav, Recent) | Task 1 |
| Permanent aside at `md+` | Task 2 |
| Mobile top bar + burger below `md` | Task 2 |
| Overlay drawer with full menu | Task 2 |
| Close: backdrop, Escape, link | Task 2 |
| Clear open on resize to `md+` | Task 2 |
| Context panel `lg+` unchanged | Task 2 (untouched classes) |
| a11y names / dialog | Task 2 |
