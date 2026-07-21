import { test } from '@playwright/test';

/**
 * Placeholder for RKSV Sonderbelege (Nullbeleg, Startbeleg, …) E2E.
 * Fiscal flows need a live TSE/TEST stack — keep skipped in default CI mocks.
 */
test.describe.skip('RKSV special receipt creation', () => {
  test('creates a Nullbeleg for a selected cash register', async () => {
    // Intentionally skipped — implement against TEST FinanzOnline / TSE environment.
  });
});
