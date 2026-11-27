import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import App from '../App.vue'
import { createMemoryHistory } from 'vue-router'
import { createAppRouter } from '../router'
import { session } from '../lib/session'

enableAutoUnmount(afterEach)
const fetchMock = vi.fn<typeof fetch>()
const email = 'operator@example.com'
const disconnectedCluster = {
  state: 'disconnected',
  engineVersion: null,
  swarmId: null,
  imported: false,
  nodes: [],
}

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  session.value = { kind: 'unknown' }
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

async function mountAt(path = '/') {
  const router = createAppRouter(createMemoryHistory())
  await router.push(path)
  const wrapper = mount(App, { global: { plugins: [router] } })
  await router.isReady()
  await flushPromises()
  return { wrapper, router }
}

describe('operator UI integration', () => {
  it('creates an account, signs in, and signs out through the real auth client', async () => {
    fetchMock.mockResolvedValueOnce(json({ setupComplete: false }))
    const { wrapper, router } = await mountAt()
    expect(router.currentRoute.value.path).toBe('/setup')
    expect(wrapper.get('h1').text()).toBe('Set up UnHost')
    expect(wrapper.text()).not.toContain('Dashboard')
    await wrapper.get('#email').setValue(` ${email} `)
    await wrapper.get('#password').setValue('Password123!')
    await wrapper.get('#setup-key').setValue('bootstrap-secret')
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 201 }))

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/auth/setup',
      expect.objectContaining({
        body: JSON.stringify({
          email,
          password: 'Password123!',
          bootstrapSecret: 'bootstrap-secret',
        }),
      }),
    )
    expect(wrapper.get('h1').text()).toBe('Sign in')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(wrapper.find('#setup-key').exists()).toBe(false)
    expect(wrapper.get('[role="status"]').text()).toContain('Account created')
    await wrapper.get('#email').setValue(email)
    await wrapper.get('#password').setValue('Password123!')
    fetchMock
      .mockResolvedValueOnce(new Response())
      .mockResolvedValueOnce(json({ email }))
      .mockResolvedValueOnce(json(disconnectedCluster))

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('h1').text()).toBe('Dashboard')
    expect(router.currentRoute.value.path).toBe('/')
    expect(wrapper.get('header a').attributes('href')).toBe('/')
    expect(wrapper.text()).toContain(email)
    expect(wrapper.text()).toContain('Local cluster')
    fetchMock.mockResolvedValueOnce(new Response())
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Sign in')
    expect(wrapper.text()).not.toContain('Dashboard')
    expect(router.currentRoute.value.path).toBe('/login')
    await router.push('/')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(wrapper.get('h1').text()).toBe('Sign in')
  })

  it('restores an existing session without showing the login form', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ setupComplete: true }))
      .mockResolvedValueOnce(json({ email }))
      .mockResolvedValueOnce(json(disconnectedCluster))
    const { wrapper, router } = await mountAt('/')
    expect(router.currentRoute.value.path).toBe('/')
    expect(wrapper.get('h1').text()).toBe('Dashboard')
    expect(wrapper.text()).toContain(email)
    expect(wrapper.text()).toContain('Local cluster')
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('hides the dashboard from an anonymous operator and prevents duplicate sign-in', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ setupComplete: true }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    const { wrapper } = await mountAt('/')
    expect(wrapper.get('h1').text()).toBe('Sign in')
    expect(wrapper.text()).not.toContain('Dashboard')
    await wrapper.get('#email').setValue(email)
    await wrapper.get('#password').setValue('wrong-password')
    let finishSignIn = () => {}
    const pending = new Promise<void>((resolve) => {
      finishSignIn = resolve
    })
    fetchMock.mockImplementationOnce(async () => {
      await pending
      return new Response(null, { status: 401 })
    })

    await wrapper.get('form').trigger('submit')
    await wrapper.get('form').trigger('submit')

    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    finishSignIn()
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('email or password is incorrect')
    expect(wrapper.get('h1').text()).toBe('Sign in')
    expect(wrapper.get('button').attributes('disabled')).toBeUndefined()
  })

  it('can retry a failed initial status request', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('offline'))
    const { wrapper } = await mountAt('/')
    expect(wrapper.get('[role="alert"]').text()).toContain('Cannot reach UnHost')
    fetchMock
      .mockResolvedValueOnce(json({ setupComplete: true }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Sign in')
  })

  it('keeps the dashboard visible when sign-out fails', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ setupComplete: true }))
      .mockResolvedValueOnce(json({ email }))
      .mockResolvedValueOnce(json(disconnectedCluster))
    const { wrapper } = await mountAt('/')
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 500 }))
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Unable to sign out')
    expect(wrapper.get('h1').text()).toBe('Dashboard')
  })

  it.each([
    {
      name: 'login before setup',
      path: '/login',
      setupComplete: false,
      signedIn: false,
      destination: '/setup',
      heading: 'Set up UnHost',
    },
    {
      name: 'dashboard before setup',
      path: '/',
      setupComplete: false,
      signedIn: false,
      destination: '/setup',
      heading: 'Set up UnHost',
    },
    {
      name: 'setup after completion',
      path: '/setup',
      setupComplete: true,
      signedIn: false,
      destination: '/login',
      heading: 'Sign in',
    },
    {
      name: 'dashboard without a session',
      path: '/',
      setupComplete: true,
      signedIn: false,
      destination: '/login',
      heading: 'Sign in',
    },
    {
      name: 'setup with a session',
      path: '/setup',
      setupComplete: true,
      signedIn: true,
      destination: '/',
      heading: 'Dashboard',
    },
    {
      name: 'login with a session',
      path: '/login',
      setupComplete: true,
      signedIn: true,
      destination: '/',
      heading: 'Dashboard',
    },
    {
      name: 'cluster URL with a session',
      path: '/cluster',
      setupComplete: true,
      signedIn: true,
      destination: '/',
      heading: 'Dashboard',
    },
  ])('redirects $name', async ({ path, setupComplete, signedIn, destination, heading }) => {
    fetchMock.mockResolvedValueOnce(json({ setupComplete }))
    if (setupComplete) {
      fetchMock.mockResolvedValueOnce(
        signedIn ? json({ email }) : new Response(null, { status: 401 }),
      )
      if (signedIn) fetchMock.mockResolvedValueOnce(json(disconnectedCluster))
    }
    const { wrapper, router } = await mountAt(path)
    expect(router.currentRoute.value.path).toBe(destination)
    expect(wrapper.get('h1').text()).toBe(heading)
  })

  it('shows a 404 for an unknown URL and lets the operator go home', async () => {
    const { wrapper, router } = await mountAt('/does/not/exist')
    expect(wrapper.get('h1').text()).toContain('404')
    expect(router.currentRoute.value.path).toBe('/does/not/exist')
    expect(fetchMock).not.toHaveBeenCalled()
    fetchMock.mockResolvedValueOnce(json({ setupComplete: false }))
    await wrapper.get('a').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/setup')
    expect(wrapper.get('h1').text()).toBe('Set up UnHost')
  })
})
