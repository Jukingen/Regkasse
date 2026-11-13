import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

test.describe('Login flow', () => {
  test.beforeEach(async ({ page }) => {
    await preparePage(page);
  });

  test('rejects invalid credentials and stays on login', async ({ page }) => {
    await loginAsAdmin(page, {
      loginIdentifier: 'admin@admin.com',
      password: 'WrongPassword!',
      expectSuccess: false,
    });

    await expect(page).toHaveURL(/\/login/);
    await expect(
      page.getByText(/falsch|invalid|fehlgeschlagen|credentials|Unauthorized/i).first()
    ).toBeVisible({ timeout: 15_000 });
  });

  test('logs in with valid credentials and reaches the admin shell', async ({ page }) => {
    await loginAsAdmin(page);
    await expectAuthenticatedShell(page);
    await expect(page.locator('#main-content')).toBeVisible();
  });
});
