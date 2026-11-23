import { expect, test } from '@playwright/test';

import {
  API,
  authHeaders,
  createPayment,
  openSaleSession,
  paymentIdOf,
  readApi,
} from '../../e2e/helpers/paymentApi';

test.describe('Payment settlement', () => {
  test.describe.configure({ timeout: 120_000 });

  test('redeems a voucher and reduces the cash due by that discount', async ({ request }) => {
    const session = await openSaleSession(request);
    const headers = authHeaders(session.token, session.tenant);
    const voucherAmount = 1;
    const cashDue = Math.round((session.product.price - voucherAmount) * 100) / 100;

    const now = new Date();
    const until = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
    const issued = await readApi(
      await request.post(`${API}/api/vouchers/issue`, {
        headers,
        data: {
          amount: 5,
          currency: 'EUR',
          validFrom: now.toISOString(),
          validUntil: until.toISOString(),
        },
      })
    );
    expect(issued.status, issued.text).toBeLessThan(300);
    const voucherCode = issued.json?.fullCode ?? issued.json?.code;
    expect(typeof voucherCode, issued.text).toBe('string');

    const paid = await createPayment(request, headers, {
      customerId: session.customerId,
      cashRegisterId: session.registerId,
      tableNumber: 72,
      totalAmount: session.product.price,
      cashGiven: cashDue,
      idempotencyKey: `e2e-voucher-${Date.now()}`,
      items: [{ productId: session.product.id, quantity: 1, taxType: 'standard' }],
      payment: {
        method: 'cash',
        tseRequired: true,
        amount: cashDue,
        voucherCode,
      },
    });

    expect(paid.status, paid.text).toBe(201);
    expect(paymentIdOf(paid.json), paid.text).toBeTruthy();
    const payment = paid.json?.payment as { totalAmount?: number } | undefined;
    expect(payment?.totalAmount, paid.text).toBe(session.product.price);

    const checked = await readApi(
      await request.post(`${API}/api/pos/vouchers/validate`, {
        headers,
        data: { voucherCode, amount: 1 },
      })
    );
    expect(checked.status, checked.text).toBeLessThan(300);
    const data = (checked.json?.data ?? checked.json) as { remainingAmount?: number };
    const remaining = data.remainingAmount ?? checked.json?.remainingAmount;
    expect(remaining, checked.text).toBe(4);
  });
});
