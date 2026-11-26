import { request } from './auth'

type EngineState = 'disconnected' | 'incompatible' | 'unavailable' | 'standalone' | 'manager' | 'worker'

interface ClusterNode {
  id: string
  hostname: string
  role: string
  readiness: string
  availability: string
}

export interface LocalCluster {
  state: EngineState
  engineVersion: string | null
  swarmId: string | null
  imported: boolean
  nodes: ClusterNode[]
}

export interface JoinCommands {
  worker: string
  manager: string
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isNode(value: unknown): value is ClusterNode {
  return isObject(value) && typeof value.id === 'string' && typeof value.hostname === 'string' &&
    typeof value.role === 'string' && typeof value.readiness === 'string' &&
    typeof value.availability === 'string'
}

function isState(value: unknown): value is EngineState {
  return value === 'disconnected' || value === 'incompatible' || value === 'unavailable' || value === 'standalone' ||
    value === 'manager' || value === 'worker'
}

async function clusterRequest(path: string, init?: RequestInit): Promise<Response> {
  const response = await request(`docker/${path}`, init, 60_000)
  if (response.ok) return response
  if (response.status === 401) throw new Error('Your session expired. Sign in again and retry.')
  const problem: unknown = await response.json().catch(() => null)
  throw new Error(isObject(problem) && typeof problem.detail === 'string'
    ? problem.detail
    : 'Unable to complete the cluster request. Please try again.')
}

export async function getCluster(): Promise<LocalCluster> {
  const response = await clusterRequest('cluster')
  const value: unknown = await response.json()
  if (!isObject(value) || !isState(value.state) ||
    (value.engineVersion !== null && typeof value.engineVersion !== 'string') ||
    (value.swarmId !== null && typeof value.swarmId !== 'string') ||
    typeof value.imported !== 'boolean' || !Array.isArray(value.nodes) || !value.nodes.every(isNode)) {
    throw new Error('UnHost returned an unexpected cluster response.')
  }
  return {
    state: value.state,
    engineVersion: value.engineVersion,
    swarmId: value.swarmId,
    imported: value.imported,
    nodes: value.nodes,
  }
}

export async function initializeCluster(advertiseAddress: string): Promise<void> {
  await clusterRequest('init', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ advertiseAddress }),
  })
}

export async function importCluster(): Promise<void> {
  await clusterRequest('import', { method: 'POST' })
}

export async function getJoinCommands(): Promise<JoinCommands> {
  const response = await clusterRequest('join-commands', { cache: 'no-store' })
  const value: unknown = await response.json()
  if (!isObject(value) || typeof value.worker !== 'string' || typeof value.manager !== 'string') {
    throw new Error('UnHost returned an unexpected join command response.')
  }
  return { worker: value.worker, manager: value.manager }
}
