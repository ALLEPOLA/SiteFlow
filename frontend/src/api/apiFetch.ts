export async function apiFetch(
  input: string,
  options: RequestInit = {},
): Promise<Response> {
  const method = (options.method ?? 'GET').toUpperCase()
  const headers = new Headers(options.headers)

  if (!['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method)) {
    const csrfResponse = await fetch('/api/auth/csrf', {
      credentials: 'same-origin',
      cache: 'no-store',
      signal: options.signal,
    })

    if (!csrfResponse.ok) {
      throw new Error('Unable to initialize a secure request.')
    }

    const csrf: { token: string } = await csrfResponse.json()

    if (!csrf.token) {
      throw new Error('Missing CSRF token.')
    }

    headers.set('X-CSRF-TOKEN', csrf.token)
  }

  return fetch(input, {
    ...options,
    method,
    headers,
    credentials: 'same-origin',
  })
}

export async function readApiResponse<T>(
  response: Response,
): Promise<T> {
  if (!response.ok) {
    const problem: {
      message?: string
      title?: string
      errors?: Record<string, string[]>
    } | null = await response.json().catch(() => null)

    const validation = problem?.errors
      ? Object.values(problem.errors).flat().join(' ')
      : ''

    const fallback =
      response.status === 401
        ? 'Your session has expired. Sign in again.'
        : response.status === 403
          ? 'You do not have permission to perform this action.'
          : `Request failed (${response.status}).`

    throw new Error(
      validation || problem?.message || problem?.title || fallback,
    )
  }

  return response.json() as Promise<T>
}