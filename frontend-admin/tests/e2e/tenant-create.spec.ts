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

  test('creates a DE tenant and shows KassenSicherheit', async ({ page }) => {
    let postedCountry: string | undefined;
    await openAuthenticated(page, '/admin/tenants/create');
    await page.route('**/api/admin/tenants', async (route) => {
      if (route.request().method() !== 'POST') {
        await route.fallback();
        return;
      }
      const body = route.request().postDataJSON() as { countryCode?: string; name?: string; slug?: string };
      postedCountry = body.countryCode;
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: '33333333-3333-4333-8333-333333333333',
          name: body.name,
          slug: body.slug,
          country: body.countryCode,
          status: 'Active',
        }),
      });
    });
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 20_000 });

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'DE');
    await dialog.getByRole('button', { name: 'Weiter' }).click();

    const formPanel = dialog.getByTestId('create-tenant-form-panel');
    const fiscalCard = formPanel.getByTestId('create-tenant-fiscal-card');
    await expect(fiscalCard).toBeVisible();
    await expect(fiscalCard).toContainText('KassenSicherheit');
    await expect(fiscalCard).not.toContainText('RKSV');
    await expect(formPanel.getByTestId('create-tenant-vat-id-hint')).toContainText('^DE\\d{9}$');

    await dialog.getByLabel(/Firmenname/i).fill('Berlin GmbH');
    const slug = dialog.getByLabel(/Subdomain|Slug/i);
    await slug.clear();
    await slug.fill('berlin-gmbh');
    await slug.blur();
    await dialog.getByLabel(/E-Mail|Kontakt/i).fill('info@berlin.example');
    await dialog.getByLabel(/E-Mail|Kontakt/i).blur();
    const submit = dialog.getByRole('button', { name: 'Kunden anlegen', exact: true });
    await expect(submit).toBeEnabled();
    await submit.click();

    await expect.poll(() => postedCountry).toBe('DE');
  });

  test('fiscal sections follow the selected country', async ({ page }) => {
    await openAuthenticated(page, '/admin/tenants/create');
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible({ timeout: 20_000 });

    await dialog.getByLabel('Land').click();
    const countryList = page.locator('.ant-select-dropdown:visible');
    await expect(countryList).toBeVisible();
    await expect(countryList).toContainText('AT — Austria');
    await expect(countryList).toContainText('DE — Germany');
    await expect(countryList).toContainText('CH — Switzerland');
    await expect(countryList).not.toContainText('EU_DEFAULT');
    await page.keyboard.press('Escape');

    const countryPanel = dialog.getByTestId('create-tenant-country-panel');

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'DE');
    await expect(countryPanel.getByTestId('fiscal-section-kassenSicherheit')).toBeVisible();
    await expect(countryPanel.getByTestId('fiscal-section-kassenSicherheit')).toContainText(
      'KassenSicherheit'
    );
    await expect(countryPanel.getByTestId('fiscal-section-rksv')).toHaveCount(0);

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'CH');
    await expect(countryPanel.getByTestId('fiscal-section-qrRechnung')).toBeVisible();
    await expect(countryPanel.getByTestId('fiscal-section-qrRechnung')).toContainText('QR-Rechnung');
    await expect(countryPanel.getByTestId('fiscal-section-rksv')).toHaveCount(0);

    await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'AT');
    const rksv = countryPanel.getByTestId('fiscal-section-rksv');
    await expect(rksv).toBeVisible();
    await expect(rksv).toContainText('RKSV / TSE');
    await expect(countryPanel.getByTestId('fiscal-section-qrRechnung')).toHaveCount(0);
    const rksvFlag = countryPanel.getByTestId('fiscal-flag-Fiscal.RksvAt');
    await expect(rksv).toContainText('Fiscal.RksvAt');
    await expect(rksvFlag).toBeChecked();
    await expect(rksvFlag).toBeDisabled();
  });
});
