export interface Operator {
  email: string
}

interface AccountCredentials {
  email: string
  password: string
}

export type Credentials =
  | (AccountCredentials & { kind: 'login' })
  | (AccountCredentials & { kind: 'setup'; bootstrapSecret: string })

async function request(path: string, init?: RequestInit): Promise<Response> {
  try {
    const response = await fetch(`/api/${path}`, {
      ...init,
      credentials: 'same-origin',
      signal: AbortSignal.timeout(15_000),
    })
    if (response.redirected) {
      throw new Error('The server redirected the request. Check the UnHost server configuration.')
    }
    return response
  } catch (error) {
    if (
      error instanceof TypeError ||
      (error instanceof DOMException && error.name === 'TimeoutError')
    ) {
      throw new Error('Cannot reach UnHost. Check that the server is running and try again.')
    }
    throw error
  }
}

export async function getSetupComplete(): Promise<boolean> {
  const response = await request('status')
  if (!response.ok) throw new Error('Unable to check setup status. Please try again.')
  const data: unknown = await response.json()
  if (
    typeof data !== 'object' ||
    data === null ||
    !('setupComplete' in data) ||
    typeof data.setupComplete !== 'boolean'
  ) {
    throw new Error('UnHost returned an unexpected status response.')
  }
  return data.setupComplete
}

export async function setup(credentials: Extract<Credentials, { kind: 'setup' }>): Promise<void> {
  const response = await request('auth/setup', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: credentials.email,
      password: credentials.password,
      bootstrapSecret: credentials.bootstrapSecret,
    }),
  })
  if (response.status === 403)
    throw new Error('Check your setup key, or reload if setup has already been completed.')
  if (response.status === 400) {
    const errors: unknown = await response.json().catch(() => null)
    if (Array.isArray(errors)) {
      const descriptions = errors.flatMap((item: unknown) =>
        typeof item === 'object' &&
        item !== null &&
        'description' in item &&
        typeof item.description === 'string'
          ? [item.description]
          : [],
      )
      if (descriptions.length) throw new Error(descriptions.join(' '))
    }
  }
  if (!response.ok)
    throw new Error('Unable to create your account. Check your details and try again.')
}

export async function getOperator(): Promise<Operator | null> {
  const response = await request('auth/me')
  if (response.status === 401) return null
  if (!response.ok) throw new Error('Unable to check your session. Please try again.')

  const data: unknown = await response.json()
  if (
    typeof data !== 'object' ||
    data === null ||
    !('email' in data) ||
    typeof data.email !== 'string'
  ) {
    throw new Error('UnHost returned an unexpected session response.')
  }
  return { email: data.email }
}

export async function login(
  credentials: Extract<Credentials, { kind: 'login' }>,
): Promise<Operator> {
  const response = await request('auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      email: credentials.email,
      password: credentials.password,
      isPersistent: false,
    }),
  })
  if (response.status === 401) throw new Error('The email or password is incorrect.')
  if (response.status === 423 || response.status === 429) {
    throw new Error('Too many sign-in attempts. Wait a few minutes and try again.')
  }
  if (response.status === 403)
    throw new Error('The server refused sign-in. Check the server’s access configuration.')
  if (!response.ok) throw new Error('Unable to sign in. Please try again.')

  const operator = await getOperator()
  if (!operator)
    throw new Error('Your session could not be saved. Allow cookies and use a secure connection.')
  return operator
}

export async function logout(): Promise<void> {
  const response = await request('auth/logout', { method: 'POST' })
  if (!response.ok && response.status !== 401) {
    throw new Error('Unable to sign out. Please try again.')
  }
}
