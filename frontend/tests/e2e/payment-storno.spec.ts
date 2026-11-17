import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  finishPaymentSuccess,
  payCash,
  pressText,
  selectTable,
  signInCashier,
  watchPayments,
} from '../../e2e/helpers/sale';

test.describe('Receipt storno', () => {
  test.describe.configure({ timeout: 180_000 });

  test('pays in cash and books a new Beleg with a negative total', async ({ page }) => {
    const payments = watchPayments(page);
    await signInCashier(page);
    await selectTable(page, 6);
    await emptyCart(page);
    await addNamedProduct(page, 'Mineralwasser');
    await payCash(page);
    await finishPaymentSuccess(page);
    await expect.poll(() => payments.length).toBeGreaterThan(0);

    const sale = payments[0];
    expect(sale.receiptNumber).toMatch(/^AT-/);
    expect(sale.total).toBeGreaterThan(0);

    await addNamedProduct(page, 'Mineralwasser');
    await page.getByRole('button', { name: /^Bezahlen/ }).click();
    await page.getByRole('button', { name: 'Storno / Rückerstattung' }).click();
    await page.getByRole('button', { name: 'VOLLSTÄNDIGE STORNIERUNG' }).click();

    const receiptField = page.getByPlaceholder(/AT-KASSE/);
    await receiptField.fill(sale.receiptNumber);
    await pressText(page, 'Beleg laden');
    await expect(page.getByText(/Originalbetrag:/)).toBeVisible({ timeout: 20_000 });
    await pressText(page, 'Ich habe verstanden und möchte den gesamten Beleg stornieren.');
    await pressText(page, 'Storno ausführen');

    await expect.poll(() => payments.length).toBeGreaterThan(1);
    const storno = payments.at(-1);
    expect(storno).toBeTruthy();
    expect(storno?.receiptNumber).toMatch(/^AT-/);
    expect(storno?.receiptNumber).not.toBe(sale.receiptNumber);
    expect(storno?.total).toBeLessThan(0);
    expect(storno?.total).toBeCloseTo(-sale.total, 2);
  });
});
