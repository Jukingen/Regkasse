import { expect, test } from '@playwright/test';

import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';
import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

const LOGIN_ROW = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  timestamp: '2026-11-20T08:00:00Z',
  action: 'USER_LOGIN',
  description: 'Kassierer hat sich angemeldet',
  userRole: 'Cashier',
  userId: '66666666-6666-4666-8666-666666666666',
  user: { id: '66666666-6666-4666-8666-666666666666', role: 'Cashier', userName: 'ada' },
};

const STORNO_ROW = {
  id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  timestamp: '2026-11-20T11:05:00Z',
  action: 'STORNO',
  description: 'Beleg storniert',
  userRole: 'Cashier',
  userId: '66666666-6666-4666-8666-666666666666',
  user: { id: '66666666-6666-4666-8666-666666666666', role: 'Cashier', userName: 'ada' },
};

test('tenant admin searches the audit log for a storno entry', async ({ page }) => {
  test.setTimeout(90_000);
  const searches: Array<string | null> = [];
  await preparePage(page);
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
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    searches.push(search || null);
    const auditLogs = [LOGIN_ROW, STORNO_ROW].filter((row) => {
      if (!search) return true;
      return `${row.action} ${row.description}`.toLowerCase().includes(search);
    });
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ auditLogs, totalCount: auditLogs.length }),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/audit-logs/activity');
  await expect(page.getByRole('heading', { name: 'Aktivitätsprotokoll' })).toBeVisible();
  await expect(page.getByText('Kassierer hat sich angemeldet')).toBeVisible();

  const search = page.getByRole('searchbox', { name: /Aktion, Entität/ });
  await search.fill('storno');
  await search.press('Enter');

  await expect.poll(() => searches.some((value) => value === 'storno')).toBeTruthy();
  await expect(page.getByText('Beleg storniert')).toBeVisible();
  await expect(page.getByText('Kassierer hat sich angemeldet')).toHaveCount(0);
});
