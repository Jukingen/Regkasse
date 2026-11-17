import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

const LOGIN_ROW = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  timestamp: '2026-11-16T08:00:00Z',
  action: 'USER_LOGIN',
  description: 'Kassierer hat sich angemeldet',
  userRole: 'Cashier',
  userId: '66666666-6666-4666-8666-666666666666',
  user: { id: '66666666-6666-4666-8666-666666666666', role: 'Cashier', userName: 'ada' },
};

const PAYMENT_ROW = {
  id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  timestamp: '2026-11-16T09:00:00Z',
  action: 'PAYMENT_CONFIRM',
  description: 'Barzahlung Espresso',
  userRole: 'Cashier',
  userId: '66666666-6666-4666-8666-666666666666',
  user: { id: '66666666-6666-4666-8666-666666666666', role: 'Cashier', userName: 'ada' },
};

test('tenant admin filters the activity log by action', async ({ page }) => {
  await preparePage(page);

  const requestedActions: Array<string | null> = [];
  await page.route('**/api/AuditLog**', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    const url = new URL(route.request().url());
    if (url.pathname !== '/api/AuditLog') {
      await route.fallback();
      return;
    }
    const action = url.searchParams.get('action');
    requestedActions.push(action);
    const auditLogs = [LOGIN_ROW, PAYMENT_ROW].filter((row) => !action || row.action === action);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ auditLogs, totalCount: auditLogs.length }),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/audit-logs/activity', { waitUntil: 'domcontentloaded' });
  await expect(page.getByText('Aktivitätsprotokoll', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('Kassierer hat sich angemeldet')).toBeVisible();
  await expect(page.getByText('Barzahlung Espresso')).toBeVisible();

  await page.locator('.ant-select').filter({ hasText: 'Aktionstyp' }).click();
  await page.locator('.ant-select-dropdown:visible').getByText('Anmeldung', { exact: true }).click();

  await expect.poll(() => requestedActions.includes('USER_LOGIN')).toBe(true);
  await expect(page.getByText('Kassierer hat sich angemeldet')).toBeVisible();
  await expect(page.getByText('Barzahlung Espresso')).toHaveCount(0);
});
