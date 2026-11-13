import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

const REGISTER_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CASHIER_ID = '66666666-6666-4666-8666-666666666666';

test.describe('Tenant admin cashier assignment', () => {
  test.describe.configure({ timeout: 90_000 });
  test('creates a cashier and assigns them to a register', async ({ page }) => {
    const register = {
      id: REGISTER_ID,
      registerNumber: 'KASSE-001',
      location: 'Theke',
      status: 'Closed',
      tenantId: '22222222-2222-4222-8222-222222222222',
      isActive: true,
      assignedUserId: null as string | null,
      assignedUserName: null as string | null,
      createdAt: '2026-01-01T00:00:00.000Z',
      updatedAt: '2026-01-01T00:00:00.000Z',
    };
    let assignedUserId: string | null = null;

    await preparePage(page);
    await page.route('**/api/admin/cash-registers/**', async (route) => {
      const url = new URL(route.request().url());
      const method = route.request().method();
      if (method === 'GET' && url.pathname === `/api/admin/cash-registers/${REGISTER_ID}`) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(register),
        });
        return;
      }
      if (method === 'POST' && url.pathname === `/api/admin/cash-registers/${REGISTER_ID}/assign`) {
        const body = route.request().postDataJSON() as { userId?: string | null };
        assignedUserId = body.userId ?? null;
        register.assignedUserId = assignedUserId;
        register.assignedUserName = assignedUserId ? 'E2E Kassierer' : null;
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(register),
        });
        return;
      }
      await route.fallback();
    });

    await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
    await expectAuthenticatedShell(page);
    await page.goto('/admin/users');
    await expect(page.getByTestId('unified-admin-users-view')).toBeVisible({ timeout: 20_000 });

    await page.getByRole('button', { name: 'Benutzer anlegen' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByLabel('E-Mail').fill(`cashier-${Date.now()}@example.com`);
    await dialog.getByLabel('Vorname').fill('E2E');
    await dialog.getByLabel('Nachname').fill('Kassierer');
    const role = dialog.getByRole('combobox', { name: '* Rolle' });
    await role.click();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await expect(dialog).toContainText('Kassierer');
    await dialog.getByRole('button', { name: 'Benutzer erstellen' }).click();

    const passwordDialog = page.getByRole('dialog', { name: 'Benutzer erfolgreich erstellt' });
    await expect(passwordDialog).toBeVisible({ timeout: 15_000 });
    await expect(passwordDialog.getByText('TenantPass1!')).toBeVisible();
    await passwordDialog.getByRole('button', { name: 'Schließen', exact: true }).click();

    await page.goto(`/admin/cash-registers/${REGISTER_ID}`);
    const assignCell = page.locator('td', {
      has: page.getByRole('combobox', { name: 'Zugewiesener Kassierer' }),
    });
    const assignee = assignCell.getByRole('combobox');
    await expect(assignee).toBeVisible({ timeout: 20_000 });
    await assignee.click();
    await page.keyboard.press('ArrowDown');
    await page.keyboard.press('Enter');
    await assignCell.getByRole('button', { name: 'Speichern' }).click();

    await expect(page.getByText('Kassierer wurde der Kasse zugewiesen.')).toBeVisible();
    await expect.poll(() => assignedUserId).toBe(CASHIER_ID);
  });
});
