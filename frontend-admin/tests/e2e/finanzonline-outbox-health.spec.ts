import { expect, test } from '@playwright/test';

import { injectAdminSession, preparePage } from './helpers/auth';

test.describe('FinanzOnline outbox health card', () => {
  test('shows pending, failed, last success, and retries failed rows for Super Admin', async ({ page }) => {
    await preparePage(page);

    await page.route('**/api/admin/dashboard/widgets', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            widgetId: 'finanzonline-outbox',
            title: 'FinanzOnline-Outbox',
            description: 'Outbox health',
            requiredPermission: 'finanzonline.view',
            defaultOrder: 27,
            defaultVisible: true,
            supportsAutoRefresh: true,
          },
        ]),
      });
    });

    await page.route('**/api/admin/dashboard/preferences', async (route) => {
      if (route.request().method() !== 'GET') {
        await route.fallback();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          widgets: [{ widgetId: 'finanzonline-outbox', order: 0, isVisible: true }],
          updatedAtUtc: '2026-11-13T08:00:00Z',
        }),
      });
    });

    await page.route('**/api/admin/finanzonline-outbox/health', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          pendingCount: 4,
          failedLast24Hours: 2,
          lastSuccessfulSubmissionUtc: '2026-11-13T08:15:00Z',
          recentErrors: [{ status: 'RetryableFailure', errorCode: 'FON-421', message: 'transient', occurredAtUtc: '2026-11-13T09:00:00Z' }],
        }),
      });
    });

    let retried = false;
    await page.route('**/api/admin/finanzonline-outbox/retry-failed', async (route) => {
      expect(route.request().method()).toBe('POST');
      retried = true;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ retriedCount: 2 }),
      });
    });

    await injectAdminSession(page);
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' });

    await expect(page.getByText('Ausstehende Übermittlungen')).toBeVisible();
    await expect(page.getByText('Fehlgeschlagen (24 Std.)')).toBeVisible();
    await expect(page.getByText('4', { exact: true })).toBeVisible();
    await expect(page.getByText('2', { exact: true })).toBeVisible();
    await expect(page.getByText(/Letzte erfolgreiche Übermittlung/)).toBeVisible();

    await page.getByTestId('fon-outbox-retry-all').click();
    await page.getByRole('button', { name: 'Alle Fehlgeschlagenen erneut senden' }).last().click();
    await expect.poll(() => retried).toBe(true);
  });
});
