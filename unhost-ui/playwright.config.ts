import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  expect: { timeout: 10_000 },
  forbidOnly: !!process.env.CI,
  workers: 1,
  outputDir: '../TestResults/browser-artifacts',
  reporter: [
    ['list'],
    ['junit', { outputFile: '../TestResults/browser.xml' }],
    ['html', { outputFolder: '../TestResults/playwright-report', open: 'never' }],
  ],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
