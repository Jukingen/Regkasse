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

async function emptyCart(page: Page): Promise<void> {
  const clear = page.getByRole('button', { name: 'Warenkorb leeren' });
  if (!(await clear.isVisible().catch(() => false))) return;
  await clear.evaluate((element) => {
    (element as HTMLElement).click();
  });
  await expect(payButton(page)).toBeDisabled({ timeout: 20_000 });
}

async function addNamedProduct(page: Page, name: string): Promise<void> {
  const product = page.getByText(name, { exact: true }).filter({ visible: true }).first();
  await expect(product).toBeVisible({ timeout: 20_000 });
  await product.click();
}

function payButton(page: Page) {
  return page.getByRole('button', { name: /^Bezahlen/ });
}

async function dismissDevToast(page: Page): Promise<void> {
  await page.evaluate(() => document.getElementById('error-toast')?.remove());
}

/** Cash dialog: pick Bar, tender enough cash, then submit. */
async function payCash(page: Page): Promise<void> {
  await dismissDevToast(page);
  const pay = payButton(page);
  await expect(pay).toBeEnabled({ timeout: 20_000 });
  await pay.click();

  const cash = page.getByRole('button', { name: /^Bar\b/ });
  await expect(cash).toBeVisible({ timeout: 20_000 });
  await cash.click();
  const preset = page.getByRole('button', { name: /^€ \d/ }).last();
  await expect(preset).toBeVisible();
  await preset.click();

  const confirm = page.getByRole('button', { name: /^Zahlen / });
  await expect(confirm).toBeEnabled({ timeout: 20_000 });
  await confirm.click();
}

test.describe('Critical POS cashier flows', () => {
  test.describe.configure({ timeout: 180_000 });

  test('pays three products in cash and shows the receipt QR code', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 1);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    await addNamedProduct(page, 'Suppe');
    await addNamedProduct(page, 'Mineralwasser');

    const fiscalBodies: string[] = [];
    page.on('response', (response) => {
      if (!response.ok() || !response.url().includes('/pos/payment')) return;
      void response
        .text()
        .then((body) => {
          if (body.includes('_R1-AT1_')) fiscalBodies.push(body);
        })
        .catch(() => undefined);
    });

    await payCash(page);

    await expect(page.getByText('Zahlung erfolgreich', { exact: true })).toBeVisible({
      timeout: 45_000,
    });
    const qr = page.getByTestId('payment-success-qr');
    await expect(qr).toBeVisible();
    await expect(qr.locator('svg, img').first()).toBeVisible();
    await expect.poll(() => fiscalBodies.length).toBeGreaterThan(0);
  });

  test('keeps table carts separate when switching tables', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 1);
    await addNamedProduct(page, 'Tagesmenü');
    await expect(page.getByText(/Tisch 1 · [1-9]/)).toBeVisible();

    await selectTable(page, 2);
    await expect(page.getByText(/Tisch 2 · 0 Artikel/)).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Tagesmenü', { exact: true })).toHaveCount(
      0
    );

    await selectTable(page, 1);
    await expect(page.getByText(/Tisch 1 · [1-9]/)).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Tagesmenü', { exact: true })).toBeVisible();
  });

  test('disables Bezahlen on an empty cart and enables it after a product is added', async ({
    page,
  }) => {
    await signIn(page, TABLES);
    await selectTable(page, 3);
    await emptyCart(page);
    const pay = payButton(page);
    await expect(pay).toBeVisible();
    await expect(pay).toBeDisabled();

    await addNamedProduct(page, 'Tagesmenü');
    await expect(pay).toBeEnabled();
  });

  test('queues a product while offline and syncs it after reconnect', async ({ page }) => {
    await signIn(page, TABLES);
    await selectTable(page, 4);
    await emptyCart(page);
    const line = page.getByTestId('pos-order-panel').getByText('Tagesmenü', { exact: true });

    await page.context().setOffline(true);
    await addNamedProduct(page, 'Tagesmenü');
    // The add is optimistic, then rolled back when the request fails. The line does not stay.
    await expect(line).toHaveCount(0);
    await expect(page.getByTestId('offline-queue-indicator')).toBeVisible();

    await page.context().setOffline(false);
    const synced = page.waitForResponse(
      (response) =>
        response.url().includes('/pos/cart/add-item') &&
        response.request().method() === 'POST' &&
        response.ok(),
      { timeout: 30_000 }
    );
    await addNamedProduct(page, 'Tagesmenü');
    await synced;
    await expect(line).toBeVisible();
  });
});
