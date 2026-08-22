import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

test.describe('Tenant creation (CreateTenantWizard)', () => {
  test.beforeEach(async ({ page }) => {
    await preparePage(page);
    await loginAsAdmin(page);
    await expectAuthenticatedShell(page);
  });

  test('opens the single-step create tenant form', async ({ page }) => {
    await page.goto('/admin/tenants/create');

    await expect(page.getByRole('heading', { name: 'Neuen Kunden (Mandant) anlegen' })).toBeVisible(
      {
        timeout: 20_000,
      }
    );

    await expect(page.getByLabel(/Firmenname/i)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Kunden anlegen', exact: true })).toBeVisible();
  });
});
