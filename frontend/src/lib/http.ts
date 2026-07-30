export type ApiError = {
  status: number
  code: string
  title: string
  detail: string | null
  fieldErrors: Record<string, string[]>
}

function buildQuery(query?: Record<string, string | number | undefined>): string {
  if (!query) {
    return ''
  }

  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === '') {
      continue
    }
    params.set(key, String(value))
  }

  const text = params.toString()
  return text === '' ? '' : `?${text}`
}

async function toApiError(response: Response): Promise<ApiError> {
  let payload: Record<string, unknown> = {}
  try {
    payload = (await response.json()) as Record<string, unknown>
  } catch {
    payload = {}
  }

  const fieldErrors: Record<string, string[]> = {}
  const errors = payload.errors
  if (errors && typeof errors === 'object') {
    for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
      if (Array.isArray(value)) {
        fieldErrors[key] = value.map(String)
      }
    }
  }

  return {
    status: response.status,
    code: typeof payload.code === 'string' ? payload.code : 'unknown',
    title: typeof payload.title === 'string' ? payload.title : response.statusText,
    detail: typeof payload.detail === 'string' ? payload.detail : null,
    fieldErrors,
  }
}

export async function apiGet<T>(
  path: string,
  query?: Record<string, string | number | undefined>,
): Promise<T> {
  const response = await fetch(`${path}${buildQuery(query)}`)
  if (!response.ok) {
    throw await toApiError(response)
  }
  return (await response.json()) as T
}

export async function apiSend<T>(
  method: 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown,
): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw await toApiError(response)
  }
  if (response.status === 204) {
    return undefined as T
  }
  const text = await response.text()
  if (text === '') {
    return undefined as T
  }
  return JSON.parse(text) as T
}
