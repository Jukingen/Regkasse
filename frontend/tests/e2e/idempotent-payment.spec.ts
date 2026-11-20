import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  finishPaymentSuccess,
  selectTable,
  signInCashier,
  watchPayments,
} from '../../e2e/helpers/sale';

test.describe('Idempotent payment', () => {
  test.describe.configure({ timeout: 180_000 });

  test('records one receipt when Bezahlen is confirmed twice', async ({ page }) => {
    const payments = watchPayments(page);
    let posts = 0;
    page.on('request', (request) => {
      if (request.method() !== 'POST') return;
      const path = new URL(request.url()).pathname.replace(/\/$/, '');
      if (path.endsWith('/api/pos/payment')) posts += 1;
    });
    await signInCashier(page);
    await selectTable(page, 6);
    await emptyCart(page);
    await addNamedProduct(page, 'Mineralwasser');

    await page.getByRole('button', { name: /^Bezahlen/ }).click();
    await page.getByRole('button', { name: /^Bar\b/ }).click();
    await page.getByRole('button', { name: /^€ \d/ }).last().click();
    const confirm = page.getByRole('button', { name: /^Zahlen / });
    await expect(confirm).toBeEnabled({ timeout: 20_000 });
    await confirm.click();
    await confirm.click({ timeout: 3_000 }).catch(() => undefined);

    await finishPaymentSuccess(page);
    await expect.poll(() => payments.length).toBe(1);
    expect(posts).toBe(1);
    expect(payments[0]?.receiptNumber).toMatch(/^AT-/);
    expect(payments[0]?.total).toBeGreaterThan(0);
  });
});
