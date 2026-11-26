import { beforeEach, expect, it, vi } from 'vitest'
import { getCluster, getJoinCommands } from '../cluster'

const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

it('rejects an incomplete node response instead of rendering malformed data', async () => {
  fetchMock.mockResolvedValueOnce(Response.json({
    state: 'manager',
    engineVersion: '28.5.0',
    swarmId: 'swarm-test',
    imported: true,
    nodes: [{ id: 'node', hostname: 'host', role: 'manager', readiness: null, availability: 'active' }],
  }))

  await expect(getCluster()).rejects.toThrow('UnHost returned an unexpected cluster response.')
})

it('requests uncached join commands and reports an expired session', async () => {
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 }))

  await expect(getJoinCommands()).rejects.toThrow('Your session expired.')
  expect(fetchMock).toHaveBeenCalledWith('/api/docker/join-commands',
    expect.objectContaining({ cache: 'no-store', credentials: 'same-origin' }))
})
