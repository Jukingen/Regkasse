import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, test, type Page } from '@playwright/test';

import { login, openCashRegister, openLogin } from '../../e2e/helpers/login';

type TestUser = { username: string; password: string };

const users = JSON.parse(
  readFileSync(path.join(__dirname, '../../e2e/fixtures/test-users.json'), 'utf8')
) as Record<string, TestUser>;

const TABLES = 'dev-gastronomy-tables';

async function signIn(page: Page, slug: string): Promise<void> {
  const account = users[slug];
  if (!account?.username || !account.password) {
    throw new Error(`Missing fixture credentials for ${slug}`);
  }
  await openLogin(page, slug);
  await login(page, account.username, account.password);
  await openCashRegister(page);
}

async function selectTable(page: Page, tableNumber: number): Promise<void> {
  const selected = page.getByRole('button', { name: `Tisch ${tableNumber}, ausgewählt` });
  if (await selected.isVisible().catch(() => false)) return;
  await page.getByRole('button', { name: `Tisch ${tableNumber} wählen` }).click();
  await expect(selected).toBeVisible();
}

async function addNamedProduct(page: Page, name: string): Promise<void> {
  const product = page.getByText(name, { exact: true }).first();
  await expect(product).toBeVisible({ timeout: 20_000 });
  await product.click();
}

test.describe('Critical POS cashier flows', () => {
  test.describe.configure({ timeout: 180_000 });

  test('pays three products in cash and shows the receipt QR code', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 1);
    await addNamedProduct(page, 'Demo Produkt 1');
    await addNamedProduct(page, 'Demo Produkt 2');
    await addNamedProduct(page, 'Demo Produkt 3');

    await page.getByRole('button', { name: 'Bezahlen' }).first().click();
    const confirm = page.getByRole('button', { name: 'Bestätigen' });
    if (await confirm.isVisible().catch(() => false)) {
      await confirm.click();
    }

    await expect(page.getByTestId('payment-success-qr')).toBeVisible({ timeout: 30_000 });
  });

  test('keeps table carts separate when switching tables', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 1);
    await addNamedProduct(page, 'Demo Produkt 1');
    await expect(page.getByText(/Tisch 1 · [1-9]/)).toBeVisible();

    await selectTable(page, 2);
    await expect(page.getByText(/Tisch 2 · 0 Artikel/)).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Demo Produkt 1', { exact: true })).toHaveCount(
      0
    );

    await selectTable(page, 1);
    await expect(page.getByText(/Tisch 1 · [1-9]/)).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Demo Produkt 1', { exact: true })).toBeVisible();
  });

  test('disables Bezahlen on an empty cart and enables it after a product is added', async ({
    page,
  }) => {
    await signIn(page, TABLES);
    await selectTable(page, 3);
    const pay = page.getByRole('button', { name: 'Bezahlen' }).first();
    await expect(pay).toBeVisible();
    await expect(pay).toBeDisabled();

    await addNamedProduct(page, 'Demo Produkt 1');
    await expect(pay).toBeEnabled();
  });

  test('queues a product while offline and syncs it after reconnect', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 4);

    await page.context().setOffline(true);
    await addNamedProduct(page, 'Demo Produkt 1');
    await expect(page.getByTestId('pos-order-panel').getByText('Demo Produkt 1', { exact: true })).toBeVisible();
    await expect(page.getByTestId('offline-queue-indicator')).toBeVisible();

    const synced = page.waitForResponse(
      (response) => response.url().includes('/pos/cart') && response.ok(),
      { timeout: 30_000 }
    );
    await page.context().setOffline(false);
    await synced;
    await expect(page.getByTestId('pos-order-panel').getByText('Demo Produkt 1', { exact: true })).toBeVisible();
  });
});
