import type { AutoMentionPosition } from './mentionConfig'
import type { ParticipantDto } from './types'

export function parseMentions(text: string, participants: ParticipantDto[]): string[] {
  const sorted = [...participants].sort((a, b) => b.name.length - a.name.length)
  const found: string[] = []
  const seen = new Set<string>()
  let i = 0
  while (i < text.length) {
    if (text[i] !== '@') {
      i += 1
      continue
    }
    const after = text.slice(i + 1)
    let matched: ParticipantDto | null = null
    for (const participant of sorted) {
      if (after.toLowerCase().startsWith(participant.name.toLowerCase())) {
        const end = participant.name.length
        const boundary = after[end]
        if (boundary === undefined || /[\s.,!?;:]/.test(boundary)) {
          matched = participant
          break
        }
      }
    }
    if (matched) {
      if (!seen.has(matched.agentId)) {
        seen.add(matched.agentId)
        found.push(matched.agentId)
      }
      i += 1 + matched.name.length
    } else {
      i += 1
    }
  }
  return found
}

export function ensureAutoMention(
  text: string,
  participant: ParticipantDto,
  position: AutoMentionPosition,
): string {
  const token = `@${participant.name}`
  if (position === 'prepend') {
    const rest = text.trimStart()
    return rest.length === 0 ? token : `${token} ${rest}`
  }
  const rest = text.trimEnd()
  return rest.length === 0 ? token : `${rest} ${token}`
}

export function mentionsMissingFromText(
  text: string,
  mentionIds: string[],
  participants: ParticipantDto[],
): ParticipantDto[] {
  const byId = new Map(participants.map((p) => [p.agentId, p]))
  const missing: ParticipantDto[] = []
  for (const id of mentionIds) {
    const participant = byId.get(id)
    if (!participant) {
      continue
    }
    const needle = `@${participant.name}`.toLowerCase()
    if (!text.toLowerCase().includes(needle)) {
      missing.push(participant)
    }
  }
  return missing
}

export function prepareOutgoingMessage(
  content: string,
  participants: ParticipantDto[],
  position: AutoMentionPosition,
): { content: string; mentions: string[] } {
  let text = content
  let mentions = parseMentions(text, participants)
  if (participants.length === 1 && mentions.length === 0) {
    text = ensureAutoMention(text, participants[0], position)
    mentions = parseMentions(text, participants)
  }
  return { content: text, mentions }
}
