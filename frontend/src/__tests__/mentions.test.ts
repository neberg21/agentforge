import { describe, expect, it } from 'vitest'
import {
  ensureAutoMention,
  mentionsMissingFromText,
  parseMentions,
  prepareOutgoingMessage,
} from '../areas/agents/mentions'
import type { ParticipantDto } from '../areas/agents/types'

const leo: ParticipantDto = { agentId: 'id-leo', name: 'leo' }
const max: ParticipantDto = { agentId: 'id-max', name: 'max' }
const leoBot: ParticipantDto = { agentId: 'id-leobot', name: 'leoBot' }

describe('parseMentions', () => {
  it('finds a single mention case-insensitively', () => {
    expect(parseMentions('hey @Leo please', [leo, max])).toEqual(['id-leo'])
  })

  it('prefers the longest matching name at a position', () => {
    expect(parseMentions('hi @leoBot', [leo, leoBot])).toEqual(['id-leobot'])
  })

  it('ignores unknown @tokens and dedupes', () => {
    expect(parseMentions('@leo @ghost @leo', [leo])).toEqual(['id-leo'])
  })

  it('returns empty when none', () => {
    expect(parseMentions('plain note', [leo])).toEqual([])
  })
})

describe('ensureAutoMention', () => {
  it('prepends @Name by default formatting', () => {
    expect(ensureAutoMention('hello', leo, 'prepend')).toBe('@leo hello')
  })

  it('appends @Name', () => {
    expect(ensureAutoMention('hello', leo, 'append')).toBe('hello @leo')
  })

  it('append on empty is just @Name', () => {
    expect(ensureAutoMention('', leo, 'append')).toBe('@leo')
  })
})

describe('mentionsMissingFromText', () => {
  it('returns participants whose @Name is absent from text', () => {
    const missing = mentionsMissingFromText('please look', ['id-leo'], [leo])
    expect(missing).toEqual([leo])
  })

  it('returns empty when @Name already in text', () => {
    const missing = mentionsMissingFromText('@leo please', ['id-leo'], [leo])
    expect(missing).toEqual([])
  })
})

describe('prepareOutgoingMessage', () => {
  it('auto-addresses 1:1', () => {
    const result = prepareOutgoingMessage('hello', [leo], 'prepend')
    expect(result.content).toBe('@leo hello')
    expect(result.mentions).toEqual(['id-leo'])
  })

  it('leaves multi-agent notes alone', () => {
    const result = prepareOutgoingMessage('hello', [leo, max], 'prepend')
    expect(result.content).toBe('hello')
    expect(result.mentions).toEqual([])
  })
})
