import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, test, type Page } from '@playwright/test';

import { login, openCashRegister, openLogin } from './helpers/login';

type TestUser = { username: string; password: string };

const users = JSON.parse(
  readFileSync(path.join(__dirname, 'fixtures', 'test-users.json'), 'utf8')
) as Record<string, TestUser>;

test.describe.configure({ timeout: 180_000 });

test('dev-doctor creates a patient and takes a cash payment', async ({ page }) => {
  const account = users['dev-doctor'];
  if (!account?.username || !account.password) {
    throw new Error('Missing fixture credentials for dev-doctor');
  }

  const patientName = `E2E Patient ${Date.now()}`;
  await openLogin(page, 'dev-doctor');
  await login(page, account.username, account.password);

  await page.getByText('Patientenakte', { exact: true }).first().click();
  await expect(page.getByLabel('Patientenname')).toBeVisible({ timeout: 20_000 });
  await page.getByLabel('Patientenname').fill(patientName);
  await page.getByLabel('Allergien').fill('Penicillin');
  await page.getByRole('button', { name: 'Patientenakte speichern' }).click();

  await openCashRegister(page);
  const product = page.getByText('Demo Produkt 1', { exact: true }).first();
  await expect(product).toBeVisible({ timeout: 20_000 });
  await product.click();
  await payCash(page);
});

async function payCash(page: Page): Promise<void> {
  const pay = page.getByRole('button', { name: 'Bezahlen' }).first();
  await expect(pay).toBeVisible({ timeout: 20_000 });
  await pay.click();
  const confirm = page.getByRole('button', { name: 'Bestätigen' });
  if (await confirm.isVisible().catch(() => false)) {
    await confirm.click();
  }
  await expect(page.getByText(/Zahlung erfolgreich|Beleg wird erstellt|Wechselgeld/)).toBeVisible({
    timeout: 30_000,
  });
}
