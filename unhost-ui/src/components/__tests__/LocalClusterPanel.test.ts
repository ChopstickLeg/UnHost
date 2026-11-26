import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import LocalClusterPanel from '../LocalClusterPanel.vue'

enableAutoUnmount(afterEach)
const fetchMock = vi.fn<typeof fetch>()

const managerCluster = {
  state: 'manager',
  engineVersion: '28.0.0',
  swarmId: 'swarm-123',
  imported: true,
  nodes: [],
}

const joinCommands = {
  worker: 'docker swarm join --token SWMTKN-1-worker 192.168.1.10:2377',
  manager: 'docker swarm join --token SWMTKN-1-manager 192.168.1.10:2377',
}

function json(data: unknown, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
})

async function mountWithJoinCommands() {
  fetchMock.mockResolvedValueOnce(json(managerCluster)).mockResolvedValueOnce(json(joinCommands))
  const wrapper = mount(LocalClusterPanel)
  await flushPromises()
  await wrapper.get('button[aria-expanded]').trigger('click')
  await flushPromises()
  return wrapper
}

describe('LocalClusterPanel join command copy buttons', () => {
  it('copies worker and manager join commands to the clipboard and shows feedback', async () => {
    const writeText = vi.fn<(text: string) => Promise<void>>().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })

    const wrapper = await mountWithJoinCommands()
    const workerButton = wrapper.get('button[aria-label="Copy worker join command"]')
    const managerButton = wrapper.get('button[aria-label="Copy manager join command"]')

    await workerButton.trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith(joinCommands.worker)
    expect(workerButton.attributes('aria-label')).toBe('Copied worker join command')
    expect(managerButton.attributes('aria-label')).toBe('Copy manager join command')

    await managerButton.trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith(joinCommands.manager)
    expect(workerButton.attributes('aria-label')).toBe('Copy worker join command')
    expect(managerButton.attributes('aria-label')).toBe('Copied manager join command')

    vi.advanceTimersByTime(2000)
    await flushPromises()

    expect(managerButton.attributes('aria-label')).toBe('Copy manager join command')
  })

  it('falls back to execCommand when navigator.clipboard is unavailable', async () => {
    vi.stubGlobal('navigator', {})
    const execCommand = vi.fn<(commandId: string) => boolean>().mockReturnValue(true)
    document.execCommand = execCommand

    const wrapper = await mountWithJoinCommands()
    const workerButton = wrapper.get('button[aria-label="Copy worker join command"]')

    await workerButton.trigger('click')
    await flushPromises()

    expect(execCommand).toHaveBeenCalledWith('copy')
    expect(workerButton.attributes('aria-label')).toBe('Copied worker join command')
  })

  it('shows an error alert when copying fails', async () => {
    vi.stubGlobal('navigator', {})
    document.execCommand = vi.fn<(commandId: string) => boolean>().mockReturnValue(false)

    const wrapper = await mountWithJoinCommands()
    await wrapper.get('button[aria-label="Copy worker join command"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('Unable to copy the join command')
  })
})

it('explains worker mode without offering cluster actions', async () => {
  fetchMock.mockResolvedValueOnce(json({
    ...managerCluster, state: 'worker', swarmId: null, imported: false,
  }))
  const wrapper = mount(LocalClusterPanel)
  await flushPromises()

  expect(wrapper.text()).toContain('Swarm worker')
  expect(wrapper.text()).toContain('Connect UnHost to a manager')
  expect(wrapper.find('#advertise-address').exists()).toBe(false)
  expect(wrapper.findAll('button').map(button => button.text())).toEqual(['Refresh'])
})

it('disables the advertised address during initialization and shows blank-detail failures', async () => {
  let finish: ((response: Response) => void) | undefined
  fetchMock
    .mockResolvedValueOnce(json({
      ...managerCluster, state: 'standalone', swarmId: null, imported: false,
    }))
    .mockImplementationOnce(() => new Promise(resolve => { finish = resolve }))
  const wrapper = mount(LocalClusterPanel)
  await flushPromises()
  const address = wrapper.get<HTMLInputElement>('#advertise-address')
  await address.setValue(' eth0 ')
  await wrapper.get('form').trigger('submit')
  expect(address.element.disabled).toBe(true)
  expect(fetchMock).toHaveBeenLastCalledWith('/api/docker/init', expect.objectContaining({
    method: 'POST', body: JSON.stringify({ advertiseAddress: 'eth0' }),
  }))

  finish?.(json({ detail: '  ' }, 422))
  await flushPromises()
  expect(address.element.disabled).toBe(false)
  expect(wrapper.get('[role="alert"]').text()).toBe('Unable to complete the cluster request. Please try again.')
})
