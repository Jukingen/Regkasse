import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, injectAdminSession, preparePage } from './helpers/auth';

const REGISTER_ID = 'reg-e2e';
const NEW_BELEG_NR = 'MB-2026-10-0042';

test('creates a past-month Monatsbeleg with force=true and shows the new BelegNr', async ({
  page,
}) => {
  let forceParam: string | null = null;

  await preparePage(page);
  await page.route('**/api/admin/rksv/monatsbelege**', async (route) => {
    const url = new URL(route.request().url());
    if (route.request().method() !== 'GET' || url.pathname !== '/api/admin/rksv/monatsbelege') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        year: 2026,
        total: 1,
        hasFailedAutoCreates: true,
        hasMissedAutoCreates: false,
        items: [
          {
            paymentId: null,
            cashRegisterId: REGISTER_ID,
            registerNumber: 'KASSE-001',
            registerLocation: 'Theke',
            year: 2026,
            month: 10,
            period: '2026-10',
            createdAtUtc: null,
            createdBy: '—',
            createdByUserId: '',
            tseSignature: '',
            depStatus: 'Missing',
            fonStatus: 'NotRequired',
            isJahresbeleg: false,
            autoCreated: false,
            status: 'failed',
            lastError: 'tse timeout',
            attemptCount: 3,
            correlationId: null,
          },
        ],
      }),
    });
  });

  await page.route('**/api/rksv/special-receipts/monatsbeleg**', async (route) => {
    const url = new URL(route.request().url());
    forceParam = url.searchParams.get('force');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        receiptNumber: NEW_BELEG_NR,
        year: 2026,
        month: 10,
        isLateCreated: true,
        daysLate: 13,
        createdAtUtc: '2026-11-13T12:00:00.000Z',
      }),
    });
  });

  await injectAdminSession(page);
  await page.goto('/rksv/monatsbelege', { waitUntil: 'domcontentloaded' });
  await expectAuthenticatedShell(page);

  await page.getByTestId('monatsbeleg-create-now-force').click();
  const dialog = page.getByRole('dialog', { name: 'Monatsbeleg nachträglich erstellen' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByText('Nachträgliche Erstellung bestätigt')).toBeVisible();
  await dialog.getByRole('button', { name: 'Nachträglich erstellen' }).click();

  await expect.poll(() => forceParam).toBe('true');
  const success = page.getByRole('dialog', { name: 'Monatsbeleg erfolgreich nachgeholt' });
  await expect(success).toBeVisible();
  await expect(success.getByText(NEW_BELEG_NR)).toBeVisible();
});
