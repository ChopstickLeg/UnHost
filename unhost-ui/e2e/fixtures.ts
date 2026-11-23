import { execFile } from 'node:child_process'
import { randomBytes, randomUUID } from 'node:crypto'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { promisify } from 'node:util'
import { test as base, expect } from '@playwright/test'

const execFileAsync = promisify(execFile)

async function docker(...args: string[]): Promise<string> {
  const { stdout } = await execFileAsync('docker', args, {
    timeout: 60_000,
    maxBuffer: 4 * 1024 * 1024,
    windowsHide: true,
  })
  return stdout.trim()
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
}

export const test = base.extend<{ installation: Installation }>({
  installation: async ({ browserName }, use, testInfo) => {
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
      '--mount', `type=bind,source=${secretPath},target=/run/secrets/bootstrap,readonly`, image,
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
          await testInfo.attach('native-container.log', { body: await docker('logs', name), contentType: 'text/plain' })
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
