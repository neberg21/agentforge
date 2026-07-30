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
