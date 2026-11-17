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

test.describe('Partial refund', () => {
  test.describe.configure({ timeout: 180_000 });

  test('refunds part of a sale and keeps tax proportional to the gross', async ({ page }) => {
    const payments = watchPayments(page);
    await signInCashier(page);
    await selectTable(page, 7);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    await addNamedProduct(page, 'Mineralwasser');
    await payCash(page);
    await finishPaymentSuccess(page);
    await expect.poll(() => payments.length).toBeGreaterThan(0);

    const sale = payments[0];
    expect(sale.total).toBeGreaterThan(1);
    expect(sale.tax).toBeGreaterThan(0);
    const refundGross = 1;

    await addNamedProduct(page, 'Mineralwasser');
    await page.getByRole('button', { name: /^Bezahlen/ }).click();
    await page.getByRole('button', { name: 'Storno / Rückerstattung' }).click();
    await page.getByRole('button', { name: 'TEILRÜCKERSTATTUNG' }).click();
    await page.getByPlaceholder(/AT-KASSE/).fill(sale.receiptNumber);
    await pressText(page, 'Positionen laden');
    const amount = page.getByRole('textbox', { name: '0,00' });
    await expect(amount).toHaveValue(/\d/, { timeout: 20_000 });
    await amount.fill(refundGross.toFixed(2));
    await expect(amount).toHaveValue(refundGross.toFixed(2));
    await pressText(page, 'Erstattung buchen');

    await expect.poll(() => payments.length).toBeGreaterThan(1);
    const refund = payments.at(-1);
    expect(refund).toBeTruthy();
    expect(refund?.receiptNumber).toMatch(/^AT-/);
    expect(refund?.receiptNumber).not.toBe(sale.receiptNumber);
    expect(refund?.total).toBeCloseTo(-refundGross, 2);
    const ratio = refundGross / sale.total;
    expect(Math.abs(refund?.tax ?? Number.NaN)).toBeCloseTo(Math.abs(sale.tax) * ratio, 2);
  });
});
