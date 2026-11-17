import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

test('tenant admin sees the Jahresbeleg card when a receipt is still missing', async ({ page }) => {
  await preparePage(page);

  await page.route('**/api/admin/dashboard/widgets', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          widgetId: 'jahresbeleg-status',
          title: 'Jahresbeleg',
          description: 'Jahresbeleg-Frist',
          requiredPermission: 'rksv.jahresbeleg.view',
          defaultOrder: 1,
          defaultVisible: true,
          supportsAutoRefresh: false,
        },
      ]),
    });
  });

  await page.route('**/api/admin/dashboard/preferences', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        widgets: [{ widgetId: 'jahresbeleg-status', order: 0, isVisible: true }],
        updatedAtUtc: '2026-11-16T08:00:00Z',
      }),
    });
  });

  await page.route('**/api/admin/jahresbeleg-reminder/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          tenantId: '22222222-2222-4222-8222-222222222222',
          tenantName: 'Dev Tenant',
          year: 2026,
          deadline: '2027-01-31',
          status: 'missing',
          warning: 'Jahresbeleg deadline is January 31. Please create it.',
        },
      ]),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

  await expect(page.getByText('Jahresbeleg', { exact: true }).first()).toBeVisible();
  await expect(page.getByTestId('jahresbeleg-status-table')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Dev Tenant', exact: true })).toBeVisible();
  await expect(page.getByText('Offen', { exact: true })).toBeVisible();
  await expect(
    page.getByText('Die Jahresbeleg-Frist ist der 31. Januar. Bitte erstellen.')
  ).toBeVisible();
  await expect(page.getByRole('link', { name: 'Jahresbeleg öffnen' })).toBeVisible();
});
