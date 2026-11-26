import { expect, test } from './fixtures'

// Join commands contain tokens, so keep them out of browser artifacts.
test.use({ withDockerEngine: true, trace: 'off', screenshot: 'off' })

for (const action of ['initialize', 'import'] as const) {
  test(`operator can ${action} a local Swarm and reopen it after restart`, async ({
    page,
    installation,
    dockerEngine,
  }) => {
    if (!dockerEngine) throw new Error('This test requires an isolated Docker Engine.')
    if (action === 'import') await dockerEngine.initialize()

    await page.goto('/')
    await page.getByLabel('Email', { exact: true }).fill('cluster@example.com')
    await page.getByLabel('Password', { exact: true }).fill('Cluster-Test123!')
    await page.getByLabel('Setup key', { exact: true }).fill(installation.bootstrapSecret)
    await page.getByRole('button', { name: 'Create account' }).click()
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible()
    await page.getByLabel('Email', { exact: true }).fill('cluster@example.com')
    await page.getByLabel('Password', { exact: true }).fill('Cluster-Test123!')
    await page.getByRole('button', { name: 'Sign in' }).click()

    if (action === 'initialize') {
      await expect(page.getByText('Standalone Engine', { exact: true })).toBeVisible()
      await page.getByLabel('Advertised address').fill(dockerEngine.address)
      await page.getByRole('button', { name: 'Initialize Swarm' }).click()
    } else {
      await expect(page.getByText('Swarm manager', { exact: true })).toBeVisible()
      await page.getByRole('button', { name: 'Import Swarm' }).click()
    }

    const imported = page.getByText('Imported into UnHost', { exact: true })
    const identity = page.getByText(/^Swarm ID \S+$/)
    await expect(imported).toBeVisible()
    const swarmId = await identity.innerText()

    if (action === 'initialize') {
      await expect.poll(async () => {
        await page.getByRole('button', { name: 'Refresh' }).click()
        await expect(imported).toBeVisible()
        const manager = page.getByRole('row').filter({
          has: page.getByRole('cell', { name: /^manager$/i }),
        })
        return manager.getByRole('cell', { name: /^ready$/i }).count()
      }).toBe(1)

      await expect(page.locator('pre code')).toHaveCount(0)
      await page.getByRole('button', { name: 'Show join commands' }).click()
      await expect(page.locator('pre code')).toHaveCount(2)
      const [worker, manager] = await page.locator('pre code').allTextContents()
      const isJoinCommand = (command?: string) =>
        !!command?.startsWith('docker swarm join --token SWMTKN-') &&
        command.endsWith(` ${dockerEngine.address}:2377`)
      expect(isJoinCommand(worker) && isJoinCommand(manager) && worker !== manager).toBe(true)
      await page.getByRole('button', { name: 'Hide join commands' }).click()
      await expect(page.locator('pre code')).toHaveCount(0)
    }

    await installation.recreate()
    await page.reload()
    await expect(imported).toBeVisible()
    await expect(identity).toHaveText(swarmId)
  })
}
