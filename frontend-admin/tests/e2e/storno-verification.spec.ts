import { expect, test } from '@playwright/test';

import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';
import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

const SALE = {
  receiptId: 'receipt-sale',
  paymentId: 'pay-sale',
  receiptNumber: 'BELEG-SALE',
  issuedAt: '2026-11-20T10:00:00.000Z',
  cashierId: 'cashier-1',
  cashRegisterId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  subTotal: 10,
  taxTotal: 2,
  grandTotal: 12,
  createdAt: '2026-11-20T10:00:00.000Z',
};

const STORNO = {
  receiptId: 'receipt-storno',
  paymentId: 'pay-storno',
  receiptNumber: 'STORNO-100',
  issuedAt: '2026-11-20T10:05:00.000Z',
  cashierId: 'cashier-1',
  cashRegisterId: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  subTotal: -10,
  taxTotal: -2,
  grandTotal: -12,
  createdAt: '2026-11-20T10:05:00.000Z',
};

test('tenant admin finds the negative storno receipt by Belegnummer', async ({ page }) => {
  test.setTimeout(90_000);
  await preparePage(page);
  await page.route('**/api/Receipts/list**', async (route) => {
    const url = new URL(route.request().url());
    const receiptNumber = url.searchParams.get('receiptNumber') ?? '';
    const items = [SALE, STORNO].filter(
      (row) => !receiptNumber || row.receiptNumber.includes(receiptNumber)
    );
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items, page: 1, pageSize: 25, totalCount: items.length }),
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/receipts');
  await expect(page.getByText('BELEG-SALE')).toBeVisible();
  await expect(page.getByText('STORNO-100')).toBeVisible();

  await page.getByPlaceholder('Belegnummer').fill('STORNO');
  await page.getByRole('button', { name: 'Suchen' }).click();

  await expect(page.getByText('STORNO-100')).toBeVisible();
  await expect(page.getByText('BELEG-SALE')).toHaveCount(0);
  await expect(page.getByRole('row', { name: /STORNO-100/ })).toContainText(/[-−]12,00/);
});
