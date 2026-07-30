import { NavLink, Outlet } from 'react-router'
import { listRecent } from '../lib/recent'
import { useContextPanel } from './ContextPanel'

export function AppShell({
  areaTitle,
  nav,
}: {
  areaTitle: string
  nav: { to: string; label: string }[]
}) {
  const { content } = useContextPanel()
  const recent = listRecent()

  return (
    <div className="flex h-full min-h-0">
      <aside className="flex w-64 shrink-0 flex-col border-r border-[var(--border)] bg-[var(--panel)] p-4">
        <div className="mb-6 text-lg font-semibold tracking-tight">AgentForge</div>
        <div className="mb-2 text-xs uppercase tracking-wide text-[var(--muted)]">{areaTitle}</div>
        <nav className="flex flex-col gap-1">
          {nav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
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
                to={
                  item.kind === 'agent'
                    ? `/agents/definitions/${item.id}`
                    : item.kind === 'run'
                      ? `/agents/runs/${item.id}`
                      : `/agents/conversations/${item.id}`
                }
                className="block truncate hover:underline"
              >
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
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
