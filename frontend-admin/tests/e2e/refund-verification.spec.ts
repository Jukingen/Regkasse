import { expect, test } from '@playwright/test';

import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';
import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

const SALE = {
  id: '11111111-1111-4111-8111-111111111111',
  transactionId: 'SALE-20',
  receiptNumber: 'BELEG-SALE-20',
  createdAt: '2026-11-20T11:00:00.000Z',
  totalAmount: 20,
  currency: 'EUR',
  method: 'Cash',
  status: 'Success',
  isStorno: false,
  isRefund: false,
};

const REFUND = {
  id: '22222222-2222-4222-8222-222222222222',
  transactionId: 'REFUND-10',
  receiptNumber: 'BELEG-REFUND-10',
  originalReceiptNumber: 'BELEG-SALE-20',
  createdAt: '2026-11-20T11:05:00.000Z',
  totalAmount: -10,
  currency: 'EUR',
  method: 'Cash',
  status: 'Refunded',
  isStorno: false,
  isRefund: true,
};

test('tenant admin filters the payment list to the partial refund', async ({ page }) => {
  test.setTimeout(90_000);
  await preparePage(page);
  await page.route('**/api/admin/payments**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    if (url.pathname.endsWith('/statistics')) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalPayments: 2,
          totalAmount: 10,
          averageAmount: 5,
          tseSignedPayments: 2,
          finanzOnlineSentPayments: 0,
          finanzOnlineSentAmount: 0,
        }),
      });
      return;
    }
    if (url.pathname !== '/api/admin/payments') {
      await route.fallback();
      return;
    }
    const refundOnly = url.searchParams.get('isRefund') === 'true';
    const items = [SALE, REFUND].filter((row) => !refundOnly || row.isRefund);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items, totalCount: items.length, page: 1, pageSize: 25 }),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/payments');
  await expect(page.getByText('SALE-20')).toBeVisible();

  await page.getByRole('button', { name: 'Erweiterte Filter' }).click();
  await page.getByRole('checkbox', { name: 'Nur Rückerstattungen' }).check();

  await expect(page.getByText('REFUND-10')).toBeVisible();
  await expect(page.getByText('SALE-20')).toHaveCount(0);
  await expect(page.getByRole('row', { name: /REFUND-10/ })).toContainText(/[-−]€\s*10,00/);
});
