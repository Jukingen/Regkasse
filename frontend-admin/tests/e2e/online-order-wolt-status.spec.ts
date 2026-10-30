import { expect, test, type Page } from '@playwright/test';

import { injectAdminSession, preparePage } from './helpers/auth';

const ORDER_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

async function allowOrderManager(page: Page) {
  await page.route('**/api/Auth/me', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: '11111111-1111-4111-8111-111111111111',
        userName: 'admin@admin.com',
        email: 'admin@admin.com',
        role: 'SuperAdmin',
        roles: ['SuperAdmin'],
        permissions: ['system.critical', 'digital.orders.view', 'digital.orders.manage'],
        isActive: true,
        mustChangePasswordOnNextLogin: false,
        tenantId: '22222222-2222-4222-8222-222222222222',
        tenantSlug: 'dev',
        tenantDisplayName: 'Dev Tenant',
        appContext: 'admin',
      }),
    });
  });
}

async function mockOrders(page: Page, actionsEnabled: boolean) {
  let status = 'pending';
  const order = () => ({
    id: ORDER_ID,
    orderNumber: 'ORD-9',
    customerName: 'Max Mustermann',
    customerPhone: '+431234567',
    orderType: 'delivery',
    subtotal: 10,
    tax: 0,
    total: 10,
    paymentMethod: 'online',
    paymentStatus: 'pending',
    orderStatus: status,
    source: 'marketplace',
    marketplaceStatusActionsEnabled: actionsEnabled,
    createdAt: '2026-10-30T10:00:00Z',
    items: [],
    statusHistory: [],
  });

  await page.route('**/api/admin/online-orders**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/analytics')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalOrders: 1,
          pending: status === 'pending' ? 1 : 0,
          completed: status === 'completed' ? 1 : 0,
          cancelled: 0,
          revenue: 0,
        }),
      });
      return;
    }

    if (route.request().method() === 'PATCH' && url.pathname.endsWith('/status')) {
      const body = route.request().postDataJSON() as { status?: string };
      status = body.status ?? status;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ succeeded: true, order: order() }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        pending: status === 'pending' ? 1 : 0,
        accepted: status === 'accepted' ? 1 : 0,
        preparing: status === 'preparing' ? 1 : 0,
        ready: status === 'ready' ? 1 : 0,
        completed: status === 'completed' ? 1 : 0,
        orders: [order()],
      }),
    });
  });
}

test.describe('Wolt kitchen status buttons', () => {
  test('advances accept, prepare, ready, and complete when the venue is connected', async ({ page }) => {
    await preparePage(page);
    await allowOrderManager(page);
    await mockOrders(page, true);
    await injectAdminSession(page);
    await page.goto('/orders/online', { waitUntil: 'domcontentloaded' });
    const row = page.getByRole('row', { name: /Max Mustermann/ }).last();
    await row.getByRole('button', { name: 'Annehmen', exact: true }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Zubereiten', exact: true }).click();
    await dialog.getByRole('button', { name: 'Bereit', exact: true }).click();
    await dialog.getByRole('button', { name: 'Abschließen', exact: true }).click();
    await expect(dialog.getByRole('button', { name: 'Abschließen' })).toHaveCount(0);
  });

  test('hides status buttons when the Wolt venue is not connected', async ({ page }) => {
    await preparePage(page);
    await allowOrderManager(page);
    await mockOrders(page, false);
    await injectAdminSession(page);
    await page.goto('/orders/online', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('Max Mustermann')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole('button', { name: 'Annehmen' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Stornieren' })).toHaveCount(0);
  });
});
