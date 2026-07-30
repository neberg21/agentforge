export type AgentDto = {
  id: string
  name: string
  description: string | null
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type BuilderSessionDto = {
  conversationId: string
  builderAgentId: string
}

export type AgentSuggestionsDto = {
  name: string
}

export type AgentSnapshotDto = {
  name: string
  systemPrompt: string
  model: string
  temperature: number
  maxOutputTokens: number
  maxTurns: number
  allowedTools: string[]
}

export type RunDto = {
  id: string
  agentId: string
  agentSnapshot: AgentSnapshotDto
  objective: string
  status: string
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  error: string | null
  promptTokens: number | null
  completionTokens: number | null
  costEstimate: number | null
  concurrencyToken: string
  conversationId?: string | null
}

export type RunMessageDto = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  createdAt: string
}

export type ParticipantDto = { agentId: string; name: string }

export type ConversationDto = {
  id: string
  title: string
  participants: ParticipantDto[]
  lastMessageExcerpt: string | null
  lastMessageAt: string | null
  createdAt: string
  updatedAt: string
  archivedAt: string | null
  concurrencyToken: string
}

export type ConversationMessageDto = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  senderAgentId: string | null
  senderName: string | null
  mentions: string[] | null
  createdAt: string
}

export type Paged<T> = { items: T[]; total: number; skip: number; take: number }

export type DraftRunDto = { objective: string; agentId: string }
