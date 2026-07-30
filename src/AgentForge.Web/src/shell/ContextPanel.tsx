import { createContext, useContext, useMemo, useState, type ReactNode } from 'react'

type ContextPanelApi = {
  content: ReactNode
  setContent: (node: ReactNode) => void
}

const ContextPanelContext = createContext<ContextPanelApi | null>(null)

export function ContextPanelProvider({ children }: { children: ReactNode }) {
  const [content, setContent] = useState<ReactNode>(null)
  const value = useMemo(() => ({ content, setContent }), [content])
  return <ContextPanelContext.Provider value={value}>{children}</ContextPanelContext.Provider>
}

export function useContextPanel(): ContextPanelApi {
  const value = useContext(ContextPanelContext)
  if (!value) {
    throw new Error('useContextPanel requires ContextPanelProvider')
  }
  return value
}
