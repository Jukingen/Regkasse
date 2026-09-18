import { type Locator, type Page, expect, test } from '@playwright/test';

import { openAuthenticated } from './helpers/auth';

const NON_AT_BANNER = 'Nicht-AT-Mandanten aktivieren RKSV/TSE nicht. Siehe docs/COUNTRIES.md.';

async function chooseSearchSelectOption(page: Page, trigger: Locator, query: string) {
  await trigger.click();
  await page.keyboard.type(query);
  await page.keyboard.press('Enter');
}

test.describe('Tenant creation (CreateTenantWizard)', () => {
  test('opens the two-step create tenant wizard', async ({ page }) => {
    await openAuthenticated(page, '/admin/tenants/create');

    await expect(page.getByRole('heading', { name: 'Neuen Kunden (Mandant) anlegen' })).toBeVisible(
      {
        timeout: 20_000,
      }
    );

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    const countryPanel = dialog.getByTestId('create-tenant-country-panel');
    const formPanel = dialog.getByTestId('create-tenant-form-panel');

    await expect(countryPanel).toBeVisible();
    await expect(dialog.getByLabel('Land')).toBeVisible();
    await expect(dialog.getByLabel('Umsatzsteuer-Regime')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Weiter' })).toBeVisible();
    await expect(formPanel).toHaveCSS('display', 'none');
    await expect(dialog.getByText(NON_AT_BANNER)).toHaveCount(0);

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'DE');
    await expect(dialog.getByText(NON_AT_BANNER)).toBeVisible();
    await page.keyboard.press('Escape');

    await dialog.getByLabel('Umsatzsteuer-Regime').click();
    await expect(page.getByRole('option', { name: 'DE_USTG_STANDARD' })).toHaveCount(1);
    await expect(page.getByRole('option', { name: 'AT_RKSV_STANDARD' })).toHaveCount(0);
    await page.keyboard.press('Escape');

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'AT');
    await expect(dialog.getByText(NON_AT_BANNER)).toHaveCount(0);

    await dialog.getByRole('button', { name: 'Weiter' }).click();

    await expect(formPanel).toHaveCSS('display', 'block');
    await expect(dialog.getByLabel(/Firmenname/i)).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Kunden anlegen', exact: true })).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Zurück' })).toBeVisible();
  });
});
