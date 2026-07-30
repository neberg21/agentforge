import { useEffect, useMemo, useState } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router'
import { areaRegistry } from './areas'
import { loadAreas, type AreaInfo } from './lib/areas'
import { AppShell } from './shell/AppShell'
import { ContextPanelProvider } from './shell/ContextPanel'

export default function App() {
  const [areas, setAreas] = useState<AreaInfo[] | null>(null)

  useEffect(() => {
    void loadAreas()
      .then(setAreas)
      .catch(() => setAreas([]))
  }, [])

  const active = useMemo(() => {
    if (!areas) {
      return []
    }
    const slugs = new Set(areas.map((area) => area.slug))
    return areaRegistry.filter((area) => slugs.has(area.slug))
  }, [areas])

  if (areas === null) {
    return <div className="p-6">AgentForge</div>
  }

  if (active.length === 0) {
    return <div className="p-6">No areas available.</div>
  }

  const first = active[0]!

  return (
    <BrowserRouter>
      <ContextPanelProvider>
        <Routes>
          <Route path="/" element={<Navigate to={`/${first.slug}`} replace />} />
          {active.map((area) => (
            <Route
              key={area.slug}
              path={`/${area.slug}/*`}
              element={<AppShell areaTitle={area.title} nav={area.nav} />}
            >
              {area.routes.map((route) =>
                route.index ? (
                  <Route key="index" index element={route.element} />
                ) : (
                  <Route key={route.path} path={route.path} element={route.element} />
                ),
              )}
            </Route>
          ))}
          <Route path="*" element={<Navigate to={`/${first.slug}`} replace />} />
        </Routes>
      </ContextPanelProvider>
    </BrowserRouter>
  )
}
