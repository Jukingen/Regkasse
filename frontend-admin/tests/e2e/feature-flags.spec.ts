import { expect, test } from '@playwright/test';

import { injectAdminSession, preparePage } from './helpers/auth';

const TENANT_ID = '22222222-2222-4222-8222-222222222222';

const FLAGS = [
  {
    name: 'EnableDepExportV2',
    enabled: false,
    configDefault: false,
    source: 'config',
    tenantOverrides: [{ tenantId: TENANT_ID, enabled: true }],
  },
  {
    name: 'EInvoicing.Peppol',
    enabled: false,
    configDefault: false,
    source: 'reserved',
    tenantOverrides: [],
  },
];

test('super admin sees every flag, toggles globally, and sets a tenant override', async ({ page }) => {
  test.setTimeout(90_000);
  await preparePage(page);

  const puts: Array<{ name?: string; enabled?: boolean; tenantId?: string | null }> = [];
  await page.route('**/api/admin/tenants**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === 'GET' && url.pathname === '/api/admin/tenants') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: TENANT_ID,
              name: 'Wien Cafe',
              slug: 'wien',
              status: 'active',
              isActive: true,
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }),
      });
      return;
    }
    await route.fallback();
  });

  await page.route('**/*', async (route) => {
    const url = route.request().url();
    if (!url.includes('/api/admin/feature-flags')) {
      await route.fallback();
      return;
    }
    if (route.request().method() === 'OPTIONS') {
      await route.fallback();
      return;
    }
    if (route.request().method() === 'PUT') {
      puts.push(route.request().postDataJSON());
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(FLAGS[0]),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(FLAGS),
    });
  });

  await injectAdminSession(page);
  await page.goto('/admin/feature-flags', { waitUntil: 'domcontentloaded' });

  await expect(page.getByText('EnableDepExportV2', { exact: true })).toBeVisible();
  await expect(page.getByText('EInvoicing.Peppol', { exact: true })).toBeVisible();
  await expect(page.getByText(/DEP-Export setzt den Header/)).toBeVisible();
  await expect(page.getByRole('switch', { name: 'EInvoicing.Peppol global' })).toBeDisabled();
  await expect(page.getByText(/Wien Cafe: An/)).toBeVisible();

  await page.getByRole('switch', { name: 'EnableDepExportV2 global' }).click();
  await expect.poll(() => puts.length).toBeGreaterThan(0);
  expect(puts.at(-1)?.tenantId ?? null).toBeNull();
  expect(puts.at(-1)).toMatchObject({ name: 'EnableDepExportV2', enabled: true, tenantId: null });

  await page.getByTestId('feature-flags-tenant-select').click();
  await page.keyboard.type('Wien Cafe');
  await page.keyboard.press('Enter');
  await page.getByRole('switch', { name: 'EnableDepExportV2 für Mandant' }).click();
  await expect.poll(() => puts.at(-1)?.tenantId).toBe(TENANT_ID);
  expect(puts.at(-1)).toMatchObject({
    name: 'EnableDepExportV2',
    enabled: false,
    tenantId: TENANT_ID,
  });
});
