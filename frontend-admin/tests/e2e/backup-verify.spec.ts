import { expect, test } from '@playwright/test';

import { e2eCredentials, isLiveE2E } from './helpers/env';

test.describe('Backup runs manual Verify', () => {
  test.skip(!isLiveE2E(), 'Requires E2E_LIVE=1, running API, and a Succeeded System backup');

  test('SuperAdmin Verify toast and detail hashes/table counts', async ({ page }) => {
    const credentials = e2eCredentials();

    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    await page.getByLabel(/E-Mail oder Benutzername/i).fill(credentials.loginIdentifier);
    await page.getByLabel(/^Passwort$/i).fill(credentials.password);

    const loginResponsePromise = page.waitForResponse(
      (response) =>
        response.url().includes('/api/Auth/login') && response.request().method() === 'POST'
    );
    await page.getByRole('button', { name: /^Anmelden$/i }).click();
    const loginResponse = await loginResponsePromise;
    expect(loginResponse.status(), `Login HTTP ${loginResponse.status()}`).toBeLessThan(400);

    await page.waitForFunction(
      () => document.cookie.split(';').some((part) => part.trim() === 'rk_admin_edge_session=1'),
      { timeout: 15_000 }
    );
    await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 30_000 });

    await page.goto('/backup/runs', { waitUntil: 'load' });
    await expect(page).not.toHaveURL(/\/login/);

    const succeededRow = page
      .locator('tr')
      .filter({ has: page.getByRole('button', { name: /^Prüfen$/i }) })
      .first();
    await expect(succeededRow).toBeVisible({ timeout: 45_000 });
    await succeededRow.getByRole('button', { name: /^Details$/i }).click();
    await page.getByRole('tab', { name: /Verifizierung/i }).click();

    const verifyInModal = page.getByRole('dialog').getByRole('button', { name: /^Prüfen$/i });
    await expect(verifyInModal).toBeVisible({ timeout: 15_000 });

    const verifyResponse = page.waitForResponse(
      (response) =>
        /\/api\/admin\/backup\/[^/]+\/verify$/i.test(response.url()) &&
        response.request().method() === 'POST'
    );
    await verifyInModal.click();
    const response = await verifyResponse;
    expect(response.ok(), `Verify HTTP ${response.status()}`).toBeTruthy();

    await expect(page.getByText('Verifizierung bestanden')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole('dialog').getByText(/LogicalDump/i)).toBeVisible();
    await expect(page.getByRole('dialog').getByText(/payment_details/i)).toBeVisible();
    await expect(page.getByRole('dialog').getByRole('cell', { name: '132' }).first()).toBeVisible();
  });
});
