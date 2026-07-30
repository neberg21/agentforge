import { Navigate, type RouteObject } from 'react-router'
import { AgentDetailPage } from './AgentDetailPage'
import { AgentFormPage } from './AgentFormPage'
import { AgentListPage } from './AgentListPage'
import { ConversationListPage, ConversationPage } from './ConversationPages'
import { RunDetailPage } from './RunDetailPage'
import { RunListPage } from './RunListPage'

export const agentsNav = [
  { to: '/agents/definitions', label: 'Agents' },
  { to: '/agents/runs', label: 'Runs' },
  { to: '/agents/conversations', label: 'Conversations' },
]

export const agentsRoutes: RouteObject[] = [
  { index: true, element: <Navigate to="definitions" replace /> },
  { path: 'definitions', element: <AgentListPage /> },
  { path: 'definitions/new', element: <AgentFormPage /> },
  { path: 'definitions/:id', element: <AgentDetailPage /> },
  { path: 'definitions/:id/edit', element: <AgentFormPage /> },
  { path: 'runs', element: <RunListPage /> },
  { path: 'runs/:id', element: <RunDetailPage /> },
  { path: 'conversations', element: <ConversationListPage /> },
  { path: 'conversations/:id', element: <ConversationPage /> },
]
