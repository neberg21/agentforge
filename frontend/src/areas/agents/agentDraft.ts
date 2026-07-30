const FENCE_RE = /```agent-draft\s*\n([\s\S]*?)```/gi

export const agentDraftDefaults = {
  model: 'gpt-4.1-mini',
  temperature: 0.7,
  maxOutputTokens: 4096,
  maxTurns: 20,
  allowedTools: [] as string[],
}

export type ValidAgentDraft = {
  name: string
  description: string | null
  systemPrompt: string
  model: string | null
  temperature: number | null
  maxOutputTokens: number | null
  maxTurns: number | null
  allowedTools: string[] | null
}

export type AgentDraftParseResult =
  | { ok: true; draft: ValidAgentDraft }
  | { ok: false; reason: 'missing' | 'invalid' }

function lastFenceMatch(content: string): RegExpExecArray | null {
  const re = new RegExp(FENCE_RE.source, FENCE_RE.flags)
  let last: RegExpExecArray | null = null
  let match: RegExpExecArray | null
  while ((match = re.exec(content)) !== null) {
    last = match
  }
  return last
}

export function stripAgentDraftFence(content: string): string {
  const last = lastFenceMatch(content)
  if (!last) {
    return content
  }
  const start = last.index
  const end = start + last[0].length
  return (content.slice(0, start) + content.slice(end)).trimEnd()
}

export function parseAgentDraft(content: string): AgentDraftParseResult {
  const last = lastFenceMatch(content)
  if (!last) {
    return { ok: false, reason: 'missing' }
  }
  try {
    const raw = JSON.parse(last[1]) as Record<string, unknown>
    const name = typeof raw.name === 'string' ? raw.name.trim() : ''
    const systemPrompt =
      typeof raw.systemPrompt === 'string' ? raw.systemPrompt.trim() : ''
    if (!name || !systemPrompt) {
      return { ok: false, reason: 'invalid' }
    }
    const description =
      raw.description === null || raw.description === undefined
        ? null
        : typeof raw.description === 'string'
          ? raw.description
          : null
    const draft: ValidAgentDraft = {
      name,
      description,
      systemPrompt,
      model: typeof raw.model === 'string' ? raw.model : null,
      temperature: typeof raw.temperature === 'number' ? raw.temperature : null,
      maxOutputTokens:
        typeof raw.maxOutputTokens === 'number' ? raw.maxOutputTokens : null,
      maxTurns: typeof raw.maxTurns === 'number' ? raw.maxTurns : null,
      allowedTools: Array.isArray(raw.allowedTools)
        ? raw.allowedTools.map(String)
        : null,
    }
    return { ok: true, draft }
  } catch {
    return { ok: false, reason: 'invalid' }
  }
}

export function toCreateAgentBody(draft: ValidAgentDraft): Record<string, unknown> {
  return {
    name: draft.name,
    description: draft.description,
    systemPrompt: draft.systemPrompt,
    model: draft.model ?? agentDraftDefaults.model,
    temperature: draft.temperature ?? agentDraftDefaults.temperature,
    maxOutputTokens: draft.maxOutputTokens ?? agentDraftDefaults.maxOutputTokens,
    maxTurns: draft.maxTurns ?? agentDraftDefaults.maxTurns,
    allowedTools: draft.allowedTools ?? agentDraftDefaults.allowedTools,
  }
}
