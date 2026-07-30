import type { RouteObject } from 'react-router'
import { agentsNav, agentsRoutes } from './agents/routes'

export type AreaModule = {
  slug: string
  title: string
  routes: RouteObject[]
  nav: { to: string; label: string }[]
}

export const areaRegistry: AreaModule[] = [
  {
    slug: 'agents',
    title: 'Agents',
    routes: agentsRoutes,
    nav: agentsNav,
  },
]
