const KEY = 'agentforge.recent'

export type RecentItem = {
  kind: 'agent' | 'run' | 'conversation'
  id: string
  label: string
}

export function rememberItem(item: RecentItem): void {
  const existing = listRecent().filter(
    (candidate) => !(candidate.kind === item.kind && candidate.id === item.id),
  )
  const next = [item, ...existing].slice(0, 5)
  localStorage.setItem(KEY, JSON.stringify(next))
}

export function listRecent(): RecentItem[] {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) {
      return []
    }
    return JSON.parse(raw) as RecentItem[]
  } catch {
    return []
  }
}

export function forgetItem(kind: RecentItem['kind'], id: string): void {
  const next = listRecent().filter((item) => !(item.kind === kind && item.id === id))
  localStorage.setItem(KEY, JSON.stringify(next))
}
