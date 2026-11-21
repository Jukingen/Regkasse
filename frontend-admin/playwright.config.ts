import { defineConfig, devices } from '@playwright/test';

const PORT = Number(process.env.E2E_PORT ?? 3000);
const baseURL = process.env.E2E_BASE_URL ?? `http://127.0.0.1:${PORT}`;

/**
 * Playwright E2E for frontend-admin.
 *
 * Default mode mocks the backend API (deterministic CI).
 * Live mode against a real API: set E2E_LIVE=1 plus E2E_ADMIN_* credentials.
 */
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  // One worker. Several admin specs failed together when Next dev dropped
  // connections under parallel load (storno, refund, voucher, audit, Fiskaly).
  workers: 1,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    locale: 'de-DE',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: process.env.E2E_SKIP_WEBSERVER
    ? undefined
    : {
        // Production server. `next dev` hot-reloads mid-suite and resets
        // in-flight pages, which showed up as connection drops.
        // A server already on this port is not reused: that process is often
        // `next dev`. Set E2E_REUSE_SERVER=1 only for a production server you started.
        command: process.env.E2E_WEB_SERVER_COMMAND ?? 'node ./scripts/e2e-web-server.mjs',
        url: baseURL,
        reuseExistingServer: process.env.E2E_REUSE_SERVER === '1',
        timeout: 720_000,
        env: {
          ...process.env,
          PORT: String(PORT),
          NEXT_PUBLIC_API_BASE_URL: process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://127.0.0.1:5184',
          NEXT_PUBLIC_RKSV_ENVIRONMENT: process.env.NEXT_PUBLIC_RKSV_ENVIRONMENT ?? 'TEST',
        },
      },
});
