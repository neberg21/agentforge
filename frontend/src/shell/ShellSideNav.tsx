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
