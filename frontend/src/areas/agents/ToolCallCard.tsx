type ToolCall = {
  id?: string
  name?: string
  arguments?: string
}

function parseToolCalls(json: string | null): ToolCall[] {
  if (!json) {
    return []
  }
  try {
    const parsed = JSON.parse(json) as unknown
    if (!Array.isArray(parsed)) {
      return []
    }
    return parsed as ToolCall[]
  } catch {
    return []
  }
}

export function ToolCallCard({ toolCallsJson }: { toolCallsJson: string | null }) {
  const calls = parseToolCalls(toolCallsJson)
  if (calls.length === 0 && !toolCallsJson) {
    return null
  }

  return (
    <details className="mt-2 rounded border border-[var(--border)] bg-[var(--bg)] p-2 text-xs">
      <summary className="cursor-pointer font-medium">Tool calls</summary>
      {calls.length === 0 ? (
        <pre className="mt-2 whitespace-pre-wrap">{toolCallsJson}</pre>
      ) : (
        <ul className="mt-2 space-y-2">
          {calls.map((call, index) => (
            <li key={call.id ?? String(index)}>
              <div className="font-medium">{call.name ?? 'tool'}</div>
              {call.arguments ? (
                <pre className="mt-1 whitespace-pre-wrap text-[var(--muted)]">{call.arguments}</pre>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </details>
  )
}
