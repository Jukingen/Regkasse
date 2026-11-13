import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

const REGISTER_A = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const REGISTER_B = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const PAYMENT_A = 'pay-a';

const RECEIPTS = [
  {
    receiptId: 'receipt-a',
    paymentId: PAYMENT_A,
    receiptNumber: 'BELEG-100',
    issuedAt: '2026-11-01T10:00:00.000Z',
    cashierId: 'cashier-1',
    cashRegisterId: REGISTER_A,
    subTotal: 10,
    taxTotal: 2,
    grandTotal: 12,
    createdAt: '2026-11-01T10:00:00.000Z',
  },
  {
    receiptId: 'receipt-b',
    paymentId: 'pay-b',
    receiptNumber: 'BELEG-200',
    issuedAt: '2026-11-02T10:00:00.000Z',
    cashierId: 'cashier-2',
    cashRegisterId: REGISTER_B,
    subTotal: 20,
    taxTotal: 4,
    grandTotal: 24,
    createdAt: '2026-11-02T10:00:00.000Z',
  },
];

test('tenant admin filters receipts by register and downloads the PDF', async ({ page }) => {
  test.setTimeout(90_000);
  let filteredRegisterId: string | null = null;

  await preparePage(page);
  await page.route('**/api/admin/cash-registers**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() === 'GET' && url.pathname === '/api/admin/cash-registers') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: REGISTER_A,
              registerNumber: 'KASSE-001',
              location: 'Theke',
              status: 'Closed',
              isActive: true,
            },
            {
              id: REGISTER_B,
              registerNumber: 'KASSE-002',
              location: 'Terrasse',
              status: 'Closed',
              isActive: true,
            },
          ],
          totalCount: 2,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        }),
      });
      return;
    }
    await route.fallback();
  });

  await page.route('**/api/Receipts/list**', async (route) => {
    const url = new URL(route.request().url());
    filteredRegisterId = url.searchParams.get('cashRegisterId');
    const items = RECEIPTS.filter(
      (row) => !filteredRegisterId || row.cashRegisterId === filteredRegisterId
    );
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items,
        page: 1,
        pageSize: 25,
        totalCount: items.length,
      }),
    });
  });

  await page.route(`**/api/admin/payments/${PAYMENT_A}/reprint**`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/pdf',
      body: '%PDF-1.4\n%E2E receipt\n',
    });
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/receipts');

  await expect(page.getByText('BELEG-100')).toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('BELEG-200')).toBeVisible();

  await page.getByRole('combobox', { name: /^Kasse/ }).click();
  await page.keyboard.type('KASSE-001');
  await page.keyboard.press('Enter');

  await expect.poll(() => filteredRegisterId).toBe(REGISTER_A);
  await expect(page.getByText('BELEG-100')).toBeVisible();
  await expect(page.getByText('BELEG-200')).toHaveCount(0);

  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Nachdruck (PDF)' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('beleg_BELEG-100.pdf');
});
