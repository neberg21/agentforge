export type TranscriptMessage = {
  id: string
  sequence: number
  role: string
  content: string | null
  toolCallsJson: string | null
  toolCallId: string | null
  senderAgentId?: string | null
  senderName?: string | null
  mentions?: string[] | null
  pending?: boolean
}

export type TranscriptState = {
  bySequence: Record<number, TranscriptMessage>
  status: string | null
  usage: { promptTokens?: number; completionTokens?: number; costEstimate?: number } | null
  error: string | null
  done: boolean
  needsMessageReload: boolean
}

export type TranscriptAction =
  | { type: 'hydrate'; messages: TranscriptMessage[] }
  | { type: 'reloadMessages'; messages: TranscriptMessage[] }
  | { type: 'sse'; event: string; data: unknown }
  | { type: 'clearReload' }

export function emptyTranscript(): TranscriptState {
  return {
    bySequence: {},
    status: null,
    usage: null,
    error: null,
    done: false,
    needsMessageReload: false,
  }
}

export function messagesInOrder(state: TranscriptState): TranscriptMessage[] {
  return Object.values(state.bySequence).sort((a, b) => a.sequence - b.sequence)
}

export function transcriptReducer(state: TranscriptState, action: TranscriptAction): TranscriptState {
  switch (action.type) {
    case 'hydrate':
    case 'reloadMessages': {
      const bySequence: Record<number, TranscriptMessage> = {}
      for (const message of action.messages) {
        bySequence[message.sequence] = message
      }
      return { ...state, bySequence, needsMessageReload: false }
    }
    case 'clearReload':
      return { ...state, needsMessageReload: false }
    case 'sse': {
      if (action.event === 'status') {
        const data = action.data as { status?: string }
        return { ...state, status: data.status ?? state.status }
      }
      if (action.event === 'usage') {
        const data = action.data as {
          promptTokens?: number
          completionTokens?: number
          costEstimate?: number
        }
        return { ...state, usage: data }
      }
      if (action.event === 'error') {
        const data = action.data as { message?: string } | null | undefined
        return { ...state, error: data?.message ?? 'error', done: true }
      }
      if (action.event === 'done') {
        return { ...state, done: true }
      }
      if (action.event === 'message') {
        return { ...state, needsMessageReload: true }
      }
      return state
    }
    default:
      return state
  }
}
