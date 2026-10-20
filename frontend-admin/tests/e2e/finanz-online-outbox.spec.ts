import { test } from '@playwright/test';

/**
 * Placeholder for /rksv/finanz-online-outbox layout.
 * The actionable outbox table (retry / mark-resolved) is covered in
 * FinanzOnlineOutboxInbox component tests. A live pass should confirm that
 * table is the first block under the page title and the 7-day summary card
 * (`fon-outbox-analytics`) follows it.
 */
test.describe.skip('FinanzOnline outbox layout', () => {
  test('shows the outbox table above the summary card', async () => {
    // Intentionally skipped — needs an authenticated FA session and outbox rows.
  });
});
