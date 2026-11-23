import { expect, test } from '@playwright/test';

import {
  API,
  SUPERADMIN_PASSWORD,
  SUPERADMIN_USER,
  authHeaders,
  cashSaleBody,
  createPayment,
  loginApi,
  openSaleSession,
  paymentIdOf,
  readApi,
  receiptNumber,
} from '../../e2e/helpers/paymentApi';

test.describe('Payment post-commit', () => {
  test.describe.configure({ timeout: 120_000 });

  test('writes the payment audit row and a FinanzOnline outbox message', async ({ request }) => {
    const session = await openSaleSession(request);
    const paid = await createPayment(
      request,
      authHeaders(session.token, session.tenant),
      cashSaleBody(session, 74, `e2e-post-${Date.now()}`)
    );
    expect(paid.status, paid.text).toBe(201);
    const paymentId = paymentIdOf(paid.json);
    const beleg = receiptNumber(paid.json);
    expect(paymentId, paid.text).toBeTruthy();
    expect(beleg, paid.text).toMatch(/^AT-/);

    const manager = authHeaders(
      await loginApi(request, SUPERADMIN_USER, 'admin', session.tenant, SUPERADMIN_PASSWORD),
      session.tenant
    );
    const audit = await readApi(
      await request.get(`${API}/api/AuditLog/payment/${paymentId}`, { headers: manager })
    );
    expect(audit.status, audit.text).toBe(200);
    const logs = (audit.json?.auditLogs ?? []) as Array<{ action?: string }>;
    expect(
      logs.some((row) => row.action === 'PaymentCreated'),
      audit.text
    ).toBeTruthy();

    await expect
      .poll(
        async () => {
          const outbox = await readApi(
            await request.get(
              `${API}/api/admin/finanzonline-outbox?businessKey=${encodeURIComponent(beleg)}&limit=20`,
              { headers: manager }
            )
          );
          if (outbox.status !== 200) return `status ${outbox.status}: ${outbox.text}`;
          const items = (outbox.json?.items ?? []) as unknown[];
          return items.length > 0 ? 'queued' : 'empty';
        },
        { timeout: 20_000, intervals: [500, 1000, 2000] }
      )
      .toBe('queued');
  });
});
