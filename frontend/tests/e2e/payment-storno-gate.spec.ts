import { expect, test } from '@playwright/test';

import { authHeaders, createPayment, openSaleSession } from '../../e2e/helpers/paymentApi';

test.describe('Payment storno gate', () => {
  test.describe.configure({ timeout: 120_000 });

  test('rejects a storno for a receipt that does not exist', async ({ request }) => {
    const session = await openSaleSession(request);
    const missing = `AT-MISSING-${Date.now()}`;
    const paid = await createPayment(request, authHeaders(session.token, session.tenant), {
      customerId: session.customerId,
      cashRegisterId: session.registerId,
      tableNumber: 75,
      totalAmount: 0.01,
      idempotencyKey: `e2e-storno-${Date.now()}`,
      isStorno: true,
      originalReceiptNumber: missing,
      stornoReason: 'KundeStorniert',
      items: [],
      payment: { method: 'cash', tseRequired: true, amount: 0.01 },
    });

    // The storno gate answers "Original receipt not found" as a payment result.
    // The create-payment mapper publishes that as HTTP 400. POST /api/pos/storno
    // is the route that returns 404, and only when the payment id itself is unknown.
    expect(paid.status, paid.text).toBe(400);
    expect(paid.json?.success, paid.text).toBe(false);
    expect(String(paid.json?.message ?? ''), paid.text).toContain('Original receipt not found');
  });
});
