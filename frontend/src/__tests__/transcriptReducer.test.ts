import { describe, expect, it } from 'vitest'
import { emptyTranscript, transcriptReducer } from '../areas/agents/transcriptReducer'

describe('transcriptReducer', () => {
  it('hydrates by sequence and clears reload flag', () => {
    let state = emptyTranscript()
    state = {
      ...state,
      needsMessageReload: true,
    }
    state = transcriptReducer(state, {
      type: 'hydrate',
      messages: [
        {
          id: '1',
          sequence: 1,
          role: 'User',
          content: 'hi',
          toolCallsJson: null,
          toolCallId: null,
        },
        {
          id: '0',
          sequence: 0,
          role: 'System',
          content: 'sys',
          toolCallsJson: null,
          toolCallId: null,
        },
      ],
    })
    expect(state.needsMessageReload).toBe(false)
    expect(Object.keys(state.bySequence)).toEqual(['0', '1'])
  })

  it('marks reload on message events', () => {
    let state = emptyTranscript()
    state = transcriptReducer(state, { type: 'sse', event: 'message', data: { role: 'Assistant' } })
    expect(state.needsMessageReload).toBe(true)
  })

  it('tracks status usage error and done', () => {
    let state = emptyTranscript()
    state = transcriptReducer(state, { type: 'sse', event: 'status', data: { status: 'Running' } })
    state = transcriptReducer(state, {
      type: 'sse',
      event: 'usage',
      data: { promptTokens: 1, completionTokens: 2 },
    })
    state = transcriptReducer(state, { type: 'sse', event: 'error', data: { message: 'boom' } })
    expect(state.status).toBe('Running')
    expect(state.usage).toEqual({ promptTokens: 1, completionTokens: 2 })
    expect(state.error).toBe('boom')
    expect(state.done).toBe(true)
  })

  it('handles error events with missing data without throwing', () => {
    const state = emptyTranscript()
    const next = transcriptReducer(state, { type: 'sse', event: 'error', data: undefined })
    expect(next.error).toBe('error')
    expect(next.done).toBe(true)
  })
})
