import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

const SUPER_ADMIN_ID = '11111111-1111-4111-8111-111111111111';
const CASHIER_ID = '66666666-6666-4666-8666-666666666666';

async function openSelect(page: import('@playwright/test').Page, label: string) {
  await page.keyboard.press('Escape');
  // Match the placeholder exactly. hasText: "Rolle" also hits the open user
  // select once its popup (user id + display name) is in the same tree.
  const select = page.locator('.ant-select').filter({
    has: page.getByText(label, { exact: true }),
  });
  await select.first().click();
}

async function expectDropdownLabels(
  page: import('@playwright/test').Page,
  present: string[],
  absent: string[]
) {
  const combobox = page.getByRole('combobox', { expanded: true }).last();
  await expect(combobox).toBeVisible();
  const listId = await combobox.getAttribute('aria-controls');
  const popup = listId
    ? page.locator('.ant-select-dropdown').filter({
        has: page.locator(`[id="${listId}"]`),
      })
    : page.locator('.ant-select-dropdown:visible').last();
  await expect(popup).toBeVisible();
  for (const value of present) {
    await expect(popup).toContainText(value);
  }
  const text = await popup.innerText();
  for (const value of absent) {
    expect(text, `dropdown text:\n${text}`).not.toContain(value);
  }
  await page.keyboard.press('Escape');
}

test('tenant admin dropdowns hide Super Admin on users, sessions, and activity', async ({
  page,
}) => {
  await preparePage(page);

  await page.route('**/api/admin/users**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === 'GET' && url.pathname === '/api/admin/users') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            userId: SUPER_ADMIN_ID,
            email: 'admin@admin.com',
            userName: 'root',
            name: 'Plattform Operator',
            role: 'SuperAdmin',
            tenantId: '22222222-2222-4222-8222-222222222222',
            tenantSlug: 'dev',
            tenantName: 'Dev Tenant',
            isActive: true,
          },
          {
            userId: CASHIER_ID,
            email: 'ada@dev.example',
            userName: 'ada',
            name: 'Ada Kassier',
            role: 'Cashier',
            tenantId: '22222222-2222-4222-8222-222222222222',
            tenantSlug: 'dev',
            tenantName: 'Dev Tenant',
            isActive: true,
          },
        ]),
      });
      return;
    }
    await route.fallback();
  });

  await page.route('**/api/UserManagement**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET' || url.pathname !== '/api/UserManagement') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: SUPER_ADMIN_ID,
            email: 'admin@admin.com',
            userName: 'root',
            firstName: 'Plattform',
            lastName: 'Operator',
            role: 'SuperAdmin',
            isActive: true,
          },
          {
            id: CASHIER_ID,
            email: 'ada@dev.example',
            userName: 'ada',
            firstName: 'Ada',
            lastName: 'Kassier',
            role: 'Cashier',
            isActive: true,
          },
        ],
        pagination: { page: 1, pageSize: 100, totalCount: 2, totalPages: 1 },
      }),
    });
  });

  await page.route('**/api/admin/sessions**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET' || url.pathname !== '/api/admin/sessions') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 'sess-super',
          userId: SUPER_ADMIN_ID,
          userName: 'root',
          displayName: 'Plattform Operator',
          role: 'SuperAdmin',
          tenantId: '22222222-2222-4222-8222-222222222222',
          isActive: true,
          isCurrent: false,
          lastActivityAtUtc: '2026-11-16T08:00:00Z',
          durationSeconds: 60,
        },
        {
          id: 'sess-ada',
          userId: CASHIER_ID,
          userName: 'ada',
          displayName: 'Ada Kassier',
          role: 'Cashier',
          tenantId: '22222222-2222-4222-8222-222222222222',
          isActive: true,
          isCurrent: false,
          lastActivityAtUtc: '2026-11-16T08:05:00Z',
          durationSeconds: 30,
        },
      ]),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);

  await page.goto('/admin/users', { waitUntil: 'domcontentloaded' });
  await expect(page.getByTestId('unified-admin-users-view')).toBeVisible();
  await expect(page.getByRole('cell', { name: 'ada', exact: true })).toBeVisible();
  await expect(page.getByText('Plattform Operator')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Rolle' }).click();
  await expectDropdownLabels(page, ['Kassierer'], ['Super-Administrator', 'SuperAdmin']);

  await page.goto('/admin/sessions', { waitUntil: 'domcontentloaded' });
  await expect(page.getByText('Aktive Sitzungen', { exact: true }).first()).toBeVisible();
  await openSelect(page, 'Rolle');
  await expectDropdownLabels(page, ['Cashier'], ['SuperAdmin']);
  await expect(page.getByRole('link', { name: 'root' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'ada' })).toBeVisible();

  await page.goto('/audit-logs/activity', { waitUntil: 'domcontentloaded' });
  await expect(page.getByText('Aktivitätsprotokoll', { exact: true }).first()).toBeVisible();
  await openSelect(page, 'Benutzer');
  await expectDropdownLabels(page, ['Ada Kassier'], ['Plattform Operator']);
  await openSelect(page, 'Rolle');
  await expectDropdownLabels(page, ['Kassierer'], ['Super-Administrator', 'SuperAdmin']);
});
