import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  finishPaymentSuccess,
  payCash,
  selectTable,
  signInCashier,
  watchPayments,
} from '../../e2e/helpers/sale';

const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

test.describe('Full cash payment', () => {
  test.describe.configure({ timeout: 180_000 });

  test('pays three products in cash and keeps the receipt QR on screen and in the PDF', async ({
    page,
  }) => {
    const payments = watchPayments(page);
    await signInCashier(page);
    await selectTable(page, 1);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    await addNamedProduct(page, 'Suppe');
    await addNamedProduct(page, 'Mineralwasser');
    await payCash(page);

    await expect(page.getByTestId('payment-success-qr')).toBeVisible({ timeout: 45_000 });
    await expect.poll(() => payments.length).toBeGreaterThan(0);
    const sale = payments[0];
    expect(sale.bodyText).toContain('_R1-AT1_');
    expect(sale.paymentId).toBeTruthy();

    const token = await page.evaluate(() => window.localStorage.getItem('token'));
    const tenant = await page.evaluate(() => window.localStorage.getItem('dev_tenant_id'));
    const pdf = await page.request.get(`${API}/api/admin/payments/${sale.paymentId}/reprint-pdf`, {
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Tenant-Id': tenant || 'dev-gastronomy-tables',
      },
    });
    const bytes = Buffer.from(await pdf.body());
    expect(pdf.ok(), bytes.subarray(0, 180).toString('utf8')).toBeTruthy();
    expect(bytes.subarray(0, 4).toString('latin1')).toBe('%PDF');
    const embedded = bytes.toString('latin1');
    expect(
      embedded.includes('/Image') || embedded.includes('PNG') || embedded.includes('_R1-AT1_'),
      embedded.slice(0, 240)
    ).toBeTruthy();

    await finishPaymentSuccess(page);
  });
});
