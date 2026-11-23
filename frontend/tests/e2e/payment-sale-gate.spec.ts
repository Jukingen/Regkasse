import { expect, test } from '@playwright/test';

import {
  authHeaders,
  cashSaleBody,
  closeShift,
  createPayment,
  diagnosticCode,
  openSaleSession,
  reopenShift,
} from '../../e2e/helpers/paymentApi';

test.describe('Payment sale gate', () => {
  test.describe.configure({ timeout: 120_000 });

  test('rejects a sale when the cash register is closed', async ({ request }) => {
    const session = await openSaleSession(request);
    const headers = authHeaders(session.token, session.tenant);
    try {
      const closed = await closeShift(request, headers);
      expect(closed.status, closed.text).toBe(200);

      const paid = await createPayment(
        request,
        headers,
        cashSaleBody(session, 71, `e2e-closed-${Date.now()}`)
      );

      // The sale gate returns CASH_REGISTER_CLOSED. HTTP 409 is reserved for cart,
      // storno, benefit, and limit conflicts; a closed register is HTTP 400.
      expect(paid.status, paid.text).toBe(400);
      expect(diagnosticCode(paid.json), paid.text).toBe('CASH_REGISTER_CLOSED');
      expect(paid.json?.success, paid.text).toBe(false);
    } finally {
      await reopenShift(request, headers, session.registerId);
    }
  });
});
