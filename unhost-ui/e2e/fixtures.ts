import { execFile } from 'node:child_process'
import { randomBytes, randomUUID } from 'node:crypto'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { promisify } from 'node:util'
import { test as base, expect } from '@playwright/test'

const execFileAsync = promisify(execFile)

function runDocker(args: string[]) {
  return execFileAsync('docker', args, {
    timeout: 60_000,
    maxBuffer: 4 * 1024 * 1024,
    windowsHide: true,
  })
}

async function docker(...args: string[]): Promise<string> {
  return (await runDocker(args)).stdout.trim()
}

async function containerLogs(name: string): Promise<string> {
  const { stdout, stderr } = await runDocker(['logs', name])
  return stdout + stderr
}

async function waitUntilReady(url: string) {
  await expect(async () => {
    const response = await fetch(`${url}/api/status`, { signal: AbortSignal.timeout(2_000) })
    expect(response.status).toBe(200)
  }).toPass({ timeout: 45_000, intervals: [200, 500, 1_000] })
}

interface Installation {
  url: string
  bootstrapSecret: string
  recreate(): Promise<void>
  logsContainJoinToken(): Promise<boolean>
}

interface DockerEngine {
  address: string
  socketVolume: string
  socketGid: string
  initialize(): Promise<void>
  withJoinedWorker(command: string, use: (hostname: string) => Promise<void>): Promise<void>
}

export const test = base.extend<{
  installation: Installation
  withDockerEngine: boolean
  dockerEngine: DockerEngine | null
}>({
  withDockerEngine: [false, { option: true }],
  dockerEngine: async ({ withDockerEngine }, use) => {
    if (!withDockerEngine) {
      await use(null)
      return
    }

    const name = `unhost-engine-${randomUUID()}`
    const socketVolume = `${name}-socket`
    const socket = '/engine/docker.sock'
    await docker('volume', 'create', socketVolume)
    try {
      await docker('create', '--privileged', '--name', name,
        '--mount', `type=volume,source=${socketVolume},target=/engine`,
        'docker:28-dind', 'dockerd', '--host', `unix://${socket}`)
      try {
        await docker('start', name)
        const engine = (...args: string[]) => docker('exec', name, 'docker', '--host', `unix://${socket}`, ...args)
        await expect(async () => {
          expect(await engine('info', '--format', '{{.ServerVersion}}')).not.toBe('')
        }).toPass({ timeout: 30_000, intervals: [250, 500, 1_000] })
        const address = await docker('inspect', '--format', '{{.NetworkSettings.Networks.bridge.IPAddress}}', name)
        const socketGid = await docker('exec', name, 'stat', '-c', '%g', socket)
        await use({
          address,
          socketVolume,
          socketGid,
          async initialize() { await engine('swarm', 'init', '--advertise-addr', address) },
          async withJoinedWorker(command, use) {
            const match = /^docker swarm join --token (SWMTKN-[\w-]+) ([\d.]+:2377)$/.exec(command)
            const token = match?.[1]
            const target = match?.[2]
            if (!token || target !== `${address}:2377`) {
              throw new Error('The displayed worker command did not target the disposable manager.')
            }

            const worker = `${name}-worker`
            await docker('create', '--privileged', '--name', worker, '--hostname', worker,
              'docker:28-dind', 'dockerd', '--host=unix:///var/run/docker.sock')
            try {
              await docker('start', worker)
              await expect(async () => {
                expect(await docker('exec', worker, 'docker', 'info', '--format', '{{.ServerVersion}}')).not.toBe('')
              }).toPass({ timeout: 30_000, intervals: [250, 500, 1_000] })
              try {
                await docker('exec', worker, 'docker', 'swarm', 'join', '--token', token, target)
              } catch {
                throw new Error('The disposable worker could not join using the displayed command.')
              }
              await use(worker)
            } finally {
              await docker('rm', '--force', '--volumes', worker)
            }
          },
        })
      } finally {
        await docker('rm', '--force', '--volumes', name)
      }
    } finally {
      await docker('volume', 'rm', socketVolume)
    }
  },
  installation: async ({ browserName, dockerEngine }, use, testInfo) => {
    const image = process.env.UNHOST_SMOKE_IMAGE
    if (!image) throw new Error('Set UNHOST_SMOKE_IMAGE to the locally built Native AOT image before running browser tests.')
    const name = `unhost-browser-${browserName}-${randomUUID()}`
    const volume = `${name}-data`
    const directory = await mkdtemp(join(tmpdir(), 'unhost-browser-'))
    const secretPath = join(directory, 'bootstrap.txt')
    const bootstrapSecret = randomBytes(32).toString('base64')
    const runContainer = (portBinding: string) => docker(
      'run', '--detach', '--name', name, '--publish', portBinding,
      '--env', 'UnHost__BootstrapSecretFile=/run/secrets/bootstrap',
      '--mount', `type=volume,source=${volume},target=/data`,
      '--mount', `type=bind,source=${secretPath},target=/run/secrets/bootstrap,readonly`,
      ...(dockerEngine ? [
        '--group-add', dockerEngine.socketGid,
        '--env', 'UnHost__DockerSocketPath=/engine/docker.sock',
        '--mount', `type=volume,source=${dockerEngine.socketVolume},target=/engine`,
      ] : []),
      image,
    )

    let fixtureFailed = false
    const cleanupErrors: unknown[] = []
    try {
      await writeFile(secretPath, bootstrapSecret, { mode: 0o644 })
      await runContainer('127.0.0.1::8080')
      const mapping = await docker('port', name, '8080/tcp')
      const port = mapping.match(/^127\.0\.0\.1:(\d+)$/)?.[1]
      if (!port) throw new Error(`Unexpected Docker port mapping: ${mapping}`)
      const url = `http://127.0.0.1:${port}`
      await waitUntilReady(url)
      await use({
        url,
        bootstrapSecret,
        async logsContainJoinToken() { return /SWMTKN-/.test(await containerLogs(name)) },
        async recreate() {
          await docker('rm', '--force', name)
          await runContainer(`127.0.0.1:${port}:8080`)
          await waitUntilReady(url)
        },
      })
    } catch (error) {
      fixtureFailed = true
      throw error
    } finally {
      const failed = fixtureFailed || testInfo.status !== testInfo.expectedStatus
      if (failed) {
        try {
          const body = (await containerLogs(name)).replace(/SWMTKN-[\w-]*/gi, '[redacted join token]')
          await testInfo.attach('native-container.log', { body, contentType: 'text/plain' })
        } catch { /* The container may have failed before it was created. */ }
      }
      for (const args of [['rm', '--force', name], ['volume', 'rm', volume]]) {
        try { await docker(...args) } catch (error) { cleanupErrors.push(error) }
      }
      try { await rm(directory, { recursive: true, force: true }) } catch (error) { cleanupErrors.push(error) }
      if (cleanupErrors.length) {
        await testInfo.attach('cleanup-errors.txt', { body: cleanupErrors.map(String).join('\n'), contentType: 'text/plain' })
      }
    }
    if (cleanupErrors.length && testInfo.status === testInfo.expectedStatus) {
      throw new AggregateError(cleanupErrors, 'Browser test cleanup failed')
    }
  },
  baseURL: async ({ installation }, use) => { await use(installation.url) },
})

export { expect }
