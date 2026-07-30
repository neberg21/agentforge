export function openEventSource(
  url: string,
  handlers: {
    onEvent: (type: string, data: unknown) => void
    onError: () => void
  },
): () => void {
  const source = new EventSource(url)
  const types = ['status', 'message', 'usage', 'error', 'done']
  for (const type of types) {
    source.addEventListener(type, (event) => {
      const message = event as MessageEvent<string>
      let data: unknown = message.data
      try {
        data = JSON.parse(message.data)
      } catch {
        // keep raw
      }
      handlers.onEvent(type, data)
    })
  }
  source.onerror = () => handlers.onError()
  return () => source.close()
}
