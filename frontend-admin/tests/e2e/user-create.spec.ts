import { type Page, expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';

async function finishDeferredCreateAndExpectPassword(page: Page) {
  const assignment = page.getByRole('dialog').filter({ hasText: 'Mandanten-Zuordnung' });
  await expect(assignment).toBeVisible({ timeout: 15_000 });
  await assignment.getByRole('button', { name: 'Schließen', exact: true }).click();

  const passwordDialog = page.getByRole('dialog', { name: 'Benutzer erfolgreich erstellt' });
  await expect(passwordDialog).toBeVisible({ timeout: 15_000 });
  await expect(passwordDialog.getByText(/Passwort|password/i).first()).toBeVisible();
}

test.describe('User creation', () => {
  test.beforeEach(async ({ page }) => {
    await preparePage(page);
    await loginAsAdmin(page);
    await expectAuthenticatedShell(page);
    await page.goto('/admin/users');
    await expect(page.getByTestId('unified-admin-users-view')).toBeVisible({ timeout: 20_000 });
  });

  test('manual create: opens modal and submits normal form', async ({ page }) => {
    await page.getByRole('button', { name: 'Benutzer anlegen' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText('Neuen Benutzer anlegen')).toBeVisible();

    await dialog.getByLabel('E-Mail').fill(`manual-${Date.now()}@example.com`);
    await dialog.getByLabel('Vorname').fill('E2E');
    await dialog.getByLabel('Nachname').fill('User');

    await dialog.getByRole('button', { name: 'Benutzer erstellen' }).click();
    await finishDeferredCreateAndExpectPassword(page);
  });

  test('quick create: switches tab and generates user', async ({ page }) => {
    await page.getByRole('button', { name: 'Benutzer anlegen' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    await dialog.getByRole('tab', { name: /Schnell anlegen|Quick/i }).click();
    await dialog.getByRole('button', { name: /generieren|Generate/i }).click();

    await finishDeferredCreateAndExpectPassword(page);
  });
});
