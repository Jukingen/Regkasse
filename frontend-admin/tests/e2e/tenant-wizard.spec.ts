import { type Locator, type Page, expect, test } from '@playwright/test';

import { openAuthenticated } from './helpers/auth';

async function chooseSearchSelectOption(page: Page, trigger: Locator, query: string) {
  await trigger.click();
  await page.keyboard.type(query);
  await page.keyboard.press('Enter');
}

test('super admin creates a tenant through the two-step wizard', async ({ page }) => {
  let postedName: string | undefined;
  const created: Array<Record<string, unknown>> = [];

  await openAuthenticated(page, '/admin/tenants/create');
  await page.route('**/api/admin/tenants**', async (route) => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    if (method === 'GET' && url.pathname === '/api/admin/tenants') {
      const items = [
        {
          id: '22222222-2222-4222-8222-222222222222',
          name: 'Dev Tenant',
          slug: 'dev',
          status: 'Active',
          isActive: true,
        },
        ...created,
      ];
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items,
          totalCount: items.length,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      });
      return;
    }
    if (method !== 'POST' || url.pathname !== '/api/admin/tenants') {
      await route.fallback();
      return;
    }
    const body = route.request().postDataJSON() as {
      name?: string;
      slug?: string;
      countryCode?: string;
      email?: string;
    };
    postedName = body.name;
    const createdTenant = {
      id: '33333333-3333-4333-8333-333333333333',
      name: body.name,
      slug: body.slug,
      email: body.email,
      country: body.countryCode ?? 'AT',
      status: 'Active',
      isActive: true,
    };
    created.push(createdTenant);
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify(createdTenant),
    });
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible({ timeout: 20_000 });
  await expect(dialog.getByTestId('create-tenant-country-panel')).toBeVisible();
  await expect(dialog.getByTestId('create-tenant-form-panel')).toHaveCSS('display', 'none');
  await expect(dialog.getByRole('button', { name: 'Weiter' })).toBeVisible();

  await chooseSearchSelectOption(page, dialog.getByLabel('Land'), 'AT');
  await dialog.getByRole('button', { name: 'Weiter' }).click();

  const formPanel = dialog.getByTestId('create-tenant-form-panel');
  await expect(formPanel).toHaveCSS('display', 'block');
  await expect(dialog.getByRole('button', { name: 'Zurück' })).toBeVisible();

  await dialog.getByLabel(/Firmenname/i).fill('Wien Cafe');
  const slug = dialog.getByLabel(/Subdomain|Slug/i);
  await slug.clear();
  await slug.fill('wien-cafe');
  await slug.blur();
  await dialog.getByLabel(/E-Mail|Kontakt/i).fill('info@wien-cafe.example');
  await dialog.getByLabel(/E-Mail|Kontakt/i).blur();

  const submit = dialog.getByRole('button', { name: 'Kunden anlegen', exact: true });
  await expect(submit).toBeEnabled();
  await submit.click();

  await expect.poll(() => postedName).toBe('Wien Cafe');
  await page.waitForURL(/\/admin\/tenants\/?$/, { timeout: 15_000 });
  await expect(page.getByRole('cell', { name: 'Wien Cafe' })).toBeVisible({ timeout: 15_000 });
});
