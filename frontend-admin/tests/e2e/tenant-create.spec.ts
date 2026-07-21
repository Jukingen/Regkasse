import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

test.describe('Tenant creation (CreateTenantWizard)', () => {
  test.beforeEach(async ({ page }) => {
    await preparePage(page);
    await loginAsAdmin(page);
    await expectAuthenticatedShell(page);
  });

  test('opens wizard and advances past tenant info step', async ({ page }) => {
    await page.goto('/admin/tenants/create');

    await expect(page.getByRole('heading', { name: 'Neuen Kunden (Mandant) anlegen' })).toBeVisible(
      {
        timeout: 20_000,
      }
    );

    const unique = Date.now().toString(36);
    const name = `Muster Cafe ${unique}`;

    await page.getByLabel(/Firmenname/i).fill(name);
    await page.getByLabel(/Firmenname/i).blur();
    await page.getByLabel(/E-Mail \(Kontakt\)/i).fill(`owner-${unique}@example.com`);

    await expect
      .poll(async () => page.getByRole('button', { name: 'Weiter', exact: true }).isEnabled(), {
        timeout: 20_000,
      })
      .toBe(true);

    await page.getByRole('button', { name: 'Weiter', exact: true }).click();

    await expect(page.getByText(/Administrator/i).first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel(/Admin.*E-Mail|Admin email/i)).toBeVisible();
  });
});
