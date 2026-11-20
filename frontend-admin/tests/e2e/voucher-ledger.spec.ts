import { expect, test } from '@playwright/test';

import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';
import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

const VOUCHER_ID = '33333333-3333-4333-8333-333333333333';

test('tenant admin sees the voucher balance after a refund ledger line', async ({ page }) => {
  test.setTimeout(90_000);
  await preparePage(page);
  await page.route('**/api/admin/vouchers**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    if (url.pathname === `/api/admin/vouchers/${VOUCHER_ID}/ledger`) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: '44444444-4444-4444-8444-444444444444',
            type: 'Refund',
            amount: 10,
            balanceAfter: 40,
            receiptNumber: 'BELEG-REFUND-10',
            createdByUserId: 'cashier-1',
            createdAtUtc: '2026-11-20T11:05:00.000Z',
          },
        ]),
      });
      return;
    }
    if (url.pathname === `/api/admin/vouchers/${VOUCHER_ID}`) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: VOUCHER_ID,
          maskedCode: 'GUT-****',
          initialAmount: 50,
          remainingAmount: 40,
          currency: 'EUR',
          status: 'PartiallyRedeemed',
          validFromUtc: '2026-11-01T00:00:00.000Z',
          expiresAtUtc: '2027-11-01T00:00:00.000Z',
          createdByUserId: 'cashier-1',
          createdAtUtc: '2026-11-01T00:00:00.000Z',
        }),
      });
      return;
    }
    if (url.pathname === '/api/admin/vouchers') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: VOUCHER_ID,
              maskedCode: 'GUT-****',
              initialAmount: 50,
              remainingAmount: 40,
              currency: 'EUR',
              status: 'PartiallyRedeemed',
              validFromUtc: '2026-11-01T00:00:00.000Z',
              expiresAtUtc: '2027-11-01T00:00:00.000Z',
              createdByUserId: 'cashier-1',
              createdAtUtc: '2026-11-01T00:00:00.000Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
        }),
      });
      return;
    }
    await route.fallback();
  });

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/vouchers');
  await page.getByRole('button', { name: 'Verlauf' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog.getByText('Buchungsverlauf')).toBeVisible();
  await expect(dialog.getByText(/Gutschrift:/)).toBeVisible();
  await expect(dialog.getByText(/Saldo danach:/)).toContainText(/40,00/);
});
