import { afterEach, describe, expect, it, vi } from 'vitest'
import { openEventSource } from '../lib/sse'

type Listener = (event: Event) => void

class FakeEventSource {
  static instances: FakeEventSource[] = []
  readonly url: string
  onerror: (() => void) | null = null
  private readonly listeners = new Map<string, Set<Listener>>()

  constructor(url: string) {
    this.url = url
    FakeEventSource.instances.push(this)
  }

  addEventListener(type: string, listener: Listener) {
    const set = this.listeners.get(type) ?? new Set<Listener>()
    set.add(listener)
    this.listeners.set(type, set)
  }

  close() {
    // no-op for tests
  }

  emit(type: string, event: Event) {
    for (const listener of this.listeners.get(type) ?? []) {
      listener(event)
    }
  }
}

afterEach(() => {
  FakeEventSource.instances = []
  vi.unstubAllGlobals()
})

describe('openEventSource', () => {
  it('ignores native EventSource connection errors for the error channel', () => {
    vi.stubGlobal('EventSource', FakeEventSource)

    const onEvent = vi.fn()
    const onError = vi.fn()
    openEventSource('/api/stream', { onEvent, onError })

    const source = FakeEventSource.instances[0]
    expect(source).toBeDefined()

    const connectionError = new Event('error')
    source.emit('error', connectionError)
    source.onerror?.()

    expect(onEvent).not.toHaveBeenCalled()
    expect(onError).toHaveBeenCalledTimes(1)
  })

  it('forwards SSE error payloads with parsed data', () => {
    vi.stubGlobal('EventSource', FakeEventSource)

    const onEvent = vi.fn()
    openEventSource('/api/stream', { onEvent, onError: () => undefined })

    const source = FakeEventSource.instances[0]
    const message = new MessageEvent('error', { data: '{"message":"boom"}' })
    source.emit('error', message)

    expect(onEvent).toHaveBeenCalledWith('error', { message: 'boom' })
  })
})
