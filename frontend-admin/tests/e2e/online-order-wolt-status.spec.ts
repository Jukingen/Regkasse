import { expect, test, type Locator, type Page, type Route } from '@playwright/test';

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

  const fulfill = async (handled: Route, body: unknown) => {
    // An aborted list refetch can leave fulfill unresolved and stall every later
    // status PATCH. Release the route handler if that happens.
    await Promise.race([
      handled
        .fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(body),
        })
        .catch(() => undefined),
      new Promise<void>((resolve) => {
        setTimeout(resolve, 1_000);
      }),
    ]);
  };

  await page.route('**/api/admin/online-orders**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (url.pathname.endsWith('/analytics')) {
      await fulfill(route, {
        totalOrders: 1,
        pending: status === 'pending' ? 1 : 0,
        completed: status === 'completed' ? 1 : 0,
        cancelled: 0,
        revenue: 0,
      });
      return;
    }

    if (request.method() === 'PATCH' && url.pathname.endsWith('/status')) {
      const raw = request.postData() ?? '{}';
      const body = JSON.parse(raw) as { status?: string };
      status = body.status ?? status;
      await fulfill(route, { succeeded: true, order: order() });
      return;
    }

    // The dialog refetches GET /online-orders/{id}. The list envelope is not an order.
    if (request.method() === 'GET' && url.pathname.endsWith(`/${ORDER_ID}`)) {
      await fulfill(route, order());
      return;
    }

    await fulfill(route, {
      pending: status === 'pending' ? 1 : 0,
      accepted: status === 'accepted' ? 1 : 0,
      preparing: status === 'preparing' ? 1 : 0,
      ready: status === 'ready' ? 1 : 0,
      completed: status === 'completed' ? 1 : 0,
      orders: [order()],
    });
  });
}

function isStatusPatch(response: { url(): string; status(): number; request(): { method(): string } }) {
  return (
    response.url().includes('/status') &&
    response.request().method() === 'PATCH' &&
    response.status() === 200
  );
}

function isOrderReloadRequest(request: { url(): string; method(): string }) {
  return (
    request.url().includes(`/online-orders/${ORDER_ID}`) &&
    !request.url().includes('/status') &&
    request.method() === 'GET'
  );
}

async function clickOnce(button: Locator) {
  const handle = await button.elementHandle();
  if (!handle) {
    throw new Error('Status button was not found.');
  }
  try {
    // elementHandle does not retry. locator.click() repeats onto the button
    // React just replaced, which submits the next kitchen status.
    await handle.evaluate((element: HTMLButtonElement) => {
      element.click();
    });
  } finally {
    await handle.dispose();
  }
}

async function clickDialogAction(page: Page, actionLabel: string, nextLabel?: string) {
  const dialog = page.getByRole('dialog');
  const button = dialog.getByRole('button', { name: new RegExp(`^(?:loading\\s+)?${actionLabel}$`) });
  await expect(button).toBeVisible({ timeout: 30_000 });
  await expect(button).toBeEnabled({ timeout: 30_000 });

  const reloaded = page.waitForRequest(isOrderReloadRequest).then(async (request) => {
    const response = await request.response();
    if (!response || response.status() !== 200) {
      throw new Error('Order dialog reload did not return HTTP 200.');
    }
  });
  await Promise.all([page.waitForResponse(isStatusPatch), clickOnce(button)]);
  await reloaded;

  if (!nextLabel) {
    await expect(button).toBeHidden({ timeout: 30_000 });
    return;
  }

  // Ant Design keeps aria-label "loading" on the icon after the button is enabled,
  // so the accessible name stays "loading Bereit". Wait until the control is enabled.
  const next = dialog.getByRole('button', { name: new RegExp(`^(?:loading\\s+)?${nextLabel}$`) });
  await expect(next).toBeVisible({ timeout: 30_000 });
  await expect(next).toBeEnabled({ timeout: 30_000 });
}

test.describe('Wolt kitchen status buttons', () => {
  test('advances accept, prepare, ready, and complete when the venue is connected', async ({ page }) => {
    await preparePage(page);
    await allowOrderManager(page);
    await mockOrders(page, true);
    await injectAdminSession(page);
    await page.goto('/orders/online', { waitUntil: 'domcontentloaded' });
    const row = page.getByRole('row', { name: /Max Mustermann/ }).last();
    const accept = row.getByRole('button', { name: 'Annehmen', exact: true });
    await expect(accept).toBeVisible({ timeout: 30_000 });
    await expect(accept).toBeEnabled({ timeout: 30_000 });
    const accepted = page.waitForResponse(isStatusPatch);
    const opened = page.waitForRequest(isOrderReloadRequest).then(async (request) => {
      const response = await request.response();
      if (!response || response.status() !== 200) {
        throw new Error('Order dialog reload did not return HTTP 200.');
      }
    });
    await Promise.all([accepted, opened, clickOnce(accept)]);
    const prepare = page.getByRole('dialog').getByRole('button', { name: /^(?:loading\s+)?Zubereiten$/ });
    await expect(prepare).toBeVisible({ timeout: 30_000 });
    await expect(prepare).toBeEnabled({ timeout: 30_000 });

    await clickDialogAction(page, 'Zubereiten', 'Bereit');
    await clickDialogAction(page, 'Bereit', 'Abschließen');
    await clickDialogAction(page, 'Abschließen');
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
