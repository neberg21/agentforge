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
