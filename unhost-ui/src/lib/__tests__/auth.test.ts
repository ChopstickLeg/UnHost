import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getOperator, getSetupComplete, login, logout, setup } from '../auth'

const fetchMock = vi.fn<typeof fetch>()
const account = { email: 'operator@example.com', password: 'Password123!' }
const loginCredentials = { kind: 'login', ...account } satisfies Parameters<typeof login>[0]
const setupCredentials = {
  kind: 'setup', ...account, bootstrapSecret: 'test-bootstrap',
} satisfies Parameters<typeof setup>[0]

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })
}

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

describe('setup status', () => {
  it.each([false, true])('reads setup state %s', async (setupComplete) => {
    fetchMock.mockResolvedValue(json({ setupComplete }))
    expect(await getSetupComplete()).toBe(setupComplete)
    expect(fetchMock).toHaveBeenCalledWith('/api/status', expect.objectContaining({ credentials: 'same-origin' }))
  })

  it.each([
    { name: 'null body', data: null },
    { name: 'missing state', data: {} },
    { name: 'string state', data: { setupComplete: 'false' } },
  ])('rejects $name', async ({ data }) => {
    fetchMock.mockResolvedValue(json(data))
    await expect(getSetupComplete()).rejects.toThrow('unexpected status response')
  })

  it('reports a status endpoint failure', async () => {
    fetchMock.mockResolvedValue(json({}, 503))
    await expect(getSetupComplete()).rejects.toThrow('Unable to check setup status')
  })
})

describe('account setup', () => {
  it('sends the bootstrap secret and account without the UI discriminator', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 201 }))
    await setup(setupCredentials)
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/setup', expect.objectContaining({
      method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...account, bootstrapSecret: 'test-bootstrap' }),
    }))
  })

  it('shows password validation descriptions and ignores malformed entries', async () => {
    fetchMock.mockResolvedValue(json([{ description: 'Password too short.' }, null, {}, { description: 42 }, { description: 'Add a number.' }], 400))
    await expect(setup(setupCredentials)).rejects.toThrow('Password too short. Add a number.')
  })

  it.each([
    { name: 'used or invalid bootstrap', status: 403, message: 'Check your setup key' },
    { name: 'unstructured validation failure', status: 400, message: 'Unable to create your account' },
    { name: 'server failure', status: 500, message: 'Unable to create your account' },
  ])('reports $name', async ({ status, message }) => {
    fetchMock.mockResolvedValue(new Response('not-json', { status }))
    await expect(setup(setupCredentials)).rejects.toThrow(message)
  })
})

describe('session', () => {
  it('returns null only for an unauthenticated session', async () => {
    fetchMock.mockResolvedValue(json({}, 401))
    expect(await getOperator()).toBeNull()
  })

  it('reads the authenticated operator', async () => {
    fetchMock.mockResolvedValue(json({ email: account.email }))
    expect(await getOperator()).toEqual({ email: account.email })
  })

  it.each([
    { name: 'null operator', data: null },
    { name: 'missing email', data: {} },
    { name: 'numeric email', data: { email: 123 } },
  ])('rejects $name', async ({ data }) => {
    fetchMock.mockResolvedValue(json(data))
    await expect(getOperator()).rejects.toThrow('unexpected session response')
  })

  it('does not treat server failure as a signed-out session', async () => {
    fetchMock.mockResolvedValue(json({}, 500))
    await expect(getOperator()).rejects.toThrow('Unable to check your session')
  })

  it('verifies the cookie was accepted after login', async () => {
    fetchMock.mockResolvedValueOnce(new Response()).mockResolvedValueOnce(json({ email: account.email }))
    expect(await login(loginCredentials)).toEqual({ email: account.email })
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/auth/login', expect.objectContaining({
      method: 'POST', body: JSON.stringify({ ...account, isPersistent: false }),
    }))
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/auth/me', expect.objectContaining({ credentials: 'same-origin' }))
  })

  it('rejects login when the session cookie was not saved', async () => {
    fetchMock.mockResolvedValueOnce(new Response()).mockResolvedValueOnce(new Response(null, { status: 401 }))
    await expect(login(loginCredentials)).rejects.toThrow('Your session could not be saved')
  })

  it.each([
    { name: 'wrong credentials', status: 401, message: 'email or password is incorrect' },
    { name: 'locked account', status: 423, message: 'Too many sign-in attempts' },
    { name: 'rate limit', status: 429, message: 'Too many sign-in attempts' },
    { name: 'access refusal', status: 403, message: 'server refused sign-in' },
    { name: 'server failure', status: 500, message: 'Unable to sign in' },
  ])('reports $name without fetching an operator', async ({ status, message }) => {
    fetchMock.mockResolvedValue(new Response(null, { status }))
    await expect(login(loginCredentials)).rejects.toThrow(message)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it.each([200, 401])('allows logout response %s', async (status) => {
    fetchMock.mockResolvedValue(new Response(null, { status }))
    await logout()
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', expect.objectContaining({ method: 'POST' }))
  })

  it('keeps logout failures visible', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 500 }))
    await expect(logout()).rejects.toThrow('Unable to sign out')
  })
})

describe('transport', () => {
  it.each([
    { name: 'network failure', cause: new TypeError('connection failed') },
    { name: 'timeout', cause: new DOMException('expired', 'TimeoutError') },
  ])('explains $name', async ({ cause }) => {
    fetchMock.mockRejectedValue(cause)
    await expect(getSetupComplete()).rejects.toThrow('Cannot reach UnHost')
  })

  it('rejects a redirect instead of accepting a login HTML page', async () => {
    const response = json({ setupComplete: true })
    Object.defineProperty(response, 'redirected', { value: true })
    fetchMock.mockResolvedValue(response)
    await expect(getSetupComplete()).rejects.toThrow('server redirected the request')
  })
})
