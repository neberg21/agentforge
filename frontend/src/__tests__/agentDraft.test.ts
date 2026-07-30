import { describe, expect, it } from 'vitest'
import {
  parseAgentDraft,
  stripAgentDraftFence,
  toCreateAgentBody,
} from '../areas/agents/agentDraft'

const fence = (json: string) =>
  `Here is your agent.\n\n\`\`\`agent-draft\n${json}\n\`\`\`\n`

describe('parseAgentDraft', () => {
  it('parses the last agent-draft fence', () => {
    const content =
      fence('{"name":"Old","systemPrompt":"old"}') +
      '\n' +
      fence(
        JSON.stringify({
          name: 'Coder',
          description: 'Writes code',
          systemPrompt: 'You write code.',
          model: null,
          temperature: null,
          maxOutputTokens: null,
          maxTurns: null,
          allowedTools: null,
        }),
      )
    const result = parseAgentDraft(content)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.draft.name).toBe('Coder')
    expect(result.draft.systemPrompt).toBe('You write code.')
    expect(result.draft.description).toBe('Writes code')
  })

  it('returns missing when no fence', () => {
    const result = parseAgentDraft('just chat')
    expect(result.ok).toBe(false)
    if (result.ok) return
    expect(result.reason).toBe('missing')
  })

  it('returns invalid when JSON is broken or required fields empty', () => {
    const broken = parseAgentDraft(fence('{'))
    expect(broken.ok).toBe(false)
    if (broken.ok) return
    expect(broken.reason).toBe('invalid')

    const emptyName = parseAgentDraft(fence('{"name":"","systemPrompt":"x"}'))
    expect(emptyName.ok).toBe(false)
    if (emptyName.ok) return
    expect(emptyName.reason).toBe('invalid')

    const emptyPrompt = parseAgentDraft(fence('{"name":"A","systemPrompt":""}'))
    expect(emptyPrompt.ok).toBe(false)
    if (emptyPrompt.ok) return
    expect(emptyPrompt.reason).toBe('invalid')
  })
})

describe('stripAgentDraftFence', () => {
  it('removes the last agent-draft fence from visible body', () => {
    const content = fence(
      '{"name":"Coder","systemPrompt":"You write code."}',
    )
    const visible = stripAgentDraftFence(content)
    expect(visible).not.toContain('agent-draft')
    expect(visible).toContain('Here is your agent.')
  })
})

describe('toCreateAgentBody', () => {
  it('fills defaults for null optional fields', () => {
    const body = toCreateAgentBody({
      name: 'Coder',
      description: null,
      systemPrompt: 'You write code.',
      model: null,
      temperature: null,
      maxOutputTokens: null,
      maxTurns: null,
      allowedTools: null,
    })
    expect(body).toEqual({
      name: 'Coder',
      description: null,
      systemPrompt: 'You write code.',
      model: 'gpt-4.1-mini',
      temperature: 0.7,
      maxOutputTokens: 4096,
      maxTurns: 20,
      allowedTools: [],
    })
  })
})
