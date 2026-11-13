import { expect, test, type Page } from '@playwright/test';

import { expectAuthenticatedShell, injectAdminSession, loginAsAdmin, preparePage } from './helpers/auth';
import { isLiveE2E } from './helpers/env';

const LIST_PATH = '/api/admin/online-payments';
const TEST_PATH = '/api/admin/online-payments/test';

type PaymentRow = {
  id: string;
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  amount: number;
  currency: string;
  status: string;
  paymentMethod: string;
  provider: string;
  paymentIntentId: string | null;
  isSynthetic: boolean;
  lastWebhookEvent: string | null;
  errorMessage: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
};

function row(overrides: Partial<PaymentRow> = {}): PaymentRow {
  return {
    id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    tenantId: '22222222-2222-4222-8222-222222222222',
    tenantName: 'Dev Tenant',
    tenantSlug: 'dev',
    amount: 10,
    currency: 'EUR',
    status: 'Pending',
    paymentMethod: 'card',
    provider: 'Mock',
    paymentIntentId: 'pi_e2e',
    isSynthetic: true,
    lastWebhookEvent: null,
    errorMessage: null,
    createdAtUtc: '2026-08-26T10:00:00.000Z',
    completedAtUtc: null,
    ...overrides,
  };
}

async function installOnlinePaymentMocks(page: Page) {
  const store: PaymentRow[] = [
    row({
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      amount: 5,
      status: 'Succeeded',
      lastWebhookEvent: 'payment_intent.succeeded',
      completedAtUtc: '2026-08-26T10:05:00.000Z',
    }),
  ];

  await page.route(`**${LIST_PATH}*`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: store,
          totalCount: store.length,
          pageNumber: 1,
          pageSize: 50,
        }),
      });
      return;
    }
    await route.fallback();
  });

  await page.route(`**${TEST_PATH}`, async (route) => {
    if (route.request().method() !== 'POST') {
      await route.fallback();
      return;
    }
    const body = route.request().postDataJSON() as {
      action?: string;
      amount?: number;
      paymentMethod?: string;
      transactionId?: string;
    };
    if (body.action === 'create') {
      const created = row({
        id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
        amount: body.amount ?? 10,
        paymentMethod: body.paymentMethod ?? 'card',
        status: 'Pending',
      });
      store.unshift(created);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ succeeded: true, transaction: created }),
      });
      return;
    }

    const targetId = body.transactionId ?? store[0]?.id;
    const idx = store.findIndex((p) => p.id === targetId);
    if (idx < 0) {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ succeeded: false, code: 'ONLINE_PAYMENT_NOT_FOUND' }),
      });
      return;
    }
    const succeeded = body.action === 'webhookSucceeded';
    const expire = body.action === 'expire';
    store[idx] = {
      ...store[idx]!,
      status: succeeded ? 'Succeeded' : 'Failed',
      lastWebhookEvent: expire
        ? 'payment_intent.canceled'
        : succeeded
          ? 'payment_intent.succeeded'
          : 'payment_intent.payment_failed',
      errorMessage: succeeded || expire ? null : 'Simulated webhook failure.',
      completedAtUtc: succeeded ? new Date().toISOString() : store[idx]!.completedAtUtc,
    };
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ succeeded: true, transaction: store[idx] }),
    });
  });
}

test.describe('Online payment admin console', () => {
  test('test console creates a payment and simulates a webhook', async ({ page }) => {
    await preparePage(page);
    if (!isLiveE2E()) {
      await installOnlinePaymentMocks(page);
      await injectAdminSession(page);
    } else {
      await loginAsAdmin(page);
    }

    await page.goto('/admin/online-payments', { waitUntil: 'domcontentloaded' });
    await expectAuthenticatedShell(page);
    await expect(page.getByRole('heading', { name: /Online-Zahlungen/i })).toBeVisible({
      timeout: 20_000,
    });

    await page.getByRole('tab', { name: 'Testkonsole' }).click();
    await expect(page.getByRole('button', { name: 'Testzahlung senden' })).toBeVisible();

    await page.getByRole('button', { name: 'Testzahlung senden' }).click();
    await expect(page.getByText(/Letzte Testzahlung:/)).toBeVisible({ timeout: 15_000 });

    const drawer = page.getByRole('dialog').filter({ hasText: /Online-Zahlung|Details/i });
    if (await drawer.isVisible().catch(() => false)) {
      await page.keyboard.press('Escape');
      await expect(drawer).toBeHidden({ timeout: 5_000 });
    }

    await page.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' }).click();
    const confirm = page.getByRole('dialog').filter({ hasText: /Webhook/i });
    if (await confirm.isVisible().catch(() => false)) {
      await confirm.getByRole('button', { name: /OK|Ja|Bestätigen/i }).click();
    }

    await expect(page.getByText(/simuliert|Erfolgreicher Webhook/i).first()).toBeVisible({
      timeout: 15_000,
    });

    await page.getByRole('tab', { name: 'Transaktionen' }).click();
    await expect(
      page.getByRole('table').getByText(/Dev Tenant|Cafe Central|Development/i).first()
    ).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByRole('table').getByText(/€\s*10/i).first()).toBeVisible();
  });

  test('gateway-console alias renders the same test console', async ({ page }) => {
    await preparePage(page);
    if (!isLiveE2E()) {
      await installOnlinePaymentMocks(page);
      await injectAdminSession(page);
    } else {
      await loginAsAdmin(page);
    }

    await page.goto('/admin/payments/gateway-console', { waitUntil: 'domcontentloaded' });
    await expectAuthenticatedShell(page);
    await expect(page.getByRole('heading', { name: /Online-Zahlungen/i })).toBeVisible({
      timeout: 20_000,
    });
    await page.getByRole('tab', { name: 'Testkonsole' }).click();
    await expect(page.getByRole('button', { name: 'Testzahlung senden' })).toBeVisible();
  });
});
