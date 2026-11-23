import { expect, test } from '@playwright/test';

import {
  authHeaders,
  cashSaleBody,
  createPayment,
  openSaleSession,
  paymentIdOf,
  qrPayload,
  receiptNumber,
} from '../../e2e/helpers/paymentApi';

test.describe('Payment document persistence', () => {
  test.describe.configure({ timeout: 120_000 });

  test('persists a Fake TSE QR payload and an Austrian BelegNr', async ({ request }) => {
    const session = await openSaleSession(request);
    const paid = await createPayment(
      request,
      authHeaders(session.token, session.tenant),
      cashSaleBody(session, 73, `e2e-beleg-${Date.now()}`)
    );

    expect(paid.status, paid.text).toBe(201);
    expect(paymentIdOf(paid.json), paid.text).toBeTruthy();
    expect(qrPayload(paid.json), paid.text).toContain('_R1-AT1_');
    expect(receiptNumber(paid.json), paid.text).toMatch(/^AT-/);
  });
});
