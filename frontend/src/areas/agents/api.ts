import { apiGet, apiSend } from '../../lib/http'
import type {
  AgentDto,
  BuilderSessionDto,
  ConversationDto,
  ConversationMessageDto,
  DraftRunDto,
  Paged,
  RunDto,
  RunMessageDto,
} from './types'

const definitions = '/api/agents/definitions'
const runs = '/api/agents/runs'
const conversations = '/api/agents/conversations'

export function listAgents(query: {
  q?: string
  skip: number
  take: number
}): Promise<Paged<AgentDto>> {
  return apiGet(definitions, {
    q: query.q === '' ? undefined : query.q,
    skip: query.skip,
    take: query.take,
  })
}

export function getAgent(id: string): Promise<AgentDto> {
  return apiGet(`${definitions}/${id}`)
}

export function createAgent(body: Record<string, unknown>): Promise<AgentDto> {
  return apiSend('POST', definitions, body)
}

export function startBuilderSession(): Promise<BuilderSessionDto> {
  return apiSend('POST', '/api/agents/builder/session')
}

export function updateAgent(id: string, body: Record<string, unknown>): Promise<AgentDto> {
  return apiSend('PUT', `${definitions}/${id}`, body)
}

export function archiveAgent(id: string): Promise<AgentDto> {
  return apiSend('DELETE', `${definitions}/${id}`)
}

export function listRuns(query: {
  agentId?: string
  status?: string
  skip: number
  take: number
}): Promise<Paged<RunDto>> {
  return apiGet(runs, query)
}

export function getRun(id: string): Promise<RunDto> {
  return apiGet(`${runs}/${id}`)
}

export function startRun(body: {
  agentId: string
  objective: string
  conversationId?: string
}): Promise<RunDto> {
  return apiSend('POST', runs, body)
}

export function cancelRun(id: string, concurrencyToken: string): Promise<RunDto> {
  return apiSend('POST', `${runs}/${id}/cancel`, { concurrencyToken })
}

export function getRunMessages(id: string): Promise<RunMessageDto[]> {
  return apiGet(`${runs}/${id}/messages`)
}

export function listConversations(query: { skip: number; take: number }): Promise<Paged<ConversationDto>> {
  return apiGet(conversations, query)
}

export function getConversation(id: string): Promise<ConversationDto> {
  return apiGet(`${conversations}/${id}`)
}

export function createConversation(body: {
  title?: string
  participantAgentIds: string[]
}): Promise<ConversationDto> {
  return apiSend('POST', conversations, body)
}

export function archiveConversation(id: string): Promise<ConversationDto> {
  return apiSend('DELETE', `${conversations}/${id}`)
}

export function getConversationMessages(id: string): Promise<ConversationMessageDto[]> {
  return apiGet(`${conversations}/${id}/messages`)
}

export function postConversationMessage(
  id: string,
  body: { content: string; mentions: string[] },
): Promise<{ streamId: string }> {
  return apiSend('POST', `${conversations}/${id}/messages`, body)
}

export function draftRun(id: string, body?: { agentId?: string }): Promise<DraftRunDto> {
  return apiSend('POST', `${conversations}/${id}/draft-run`, body ?? {})
}
