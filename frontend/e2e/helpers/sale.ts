import { readFileSync } from 'node:fs';
import path from 'node:path';

import { expect, type Page, type Response } from '@playwright/test';

import { login, openCashRegister, openLogin } from './login';

type TestUser = { username: string; password: string };

const users = JSON.parse(
  readFileSync(path.join(__dirname, '../fixtures/test-users.json'), 'utf8')
) as Record<string, TestUser>;

export const TABLES_TENANT = 'dev-gastronomy-tables';

type PaymentSnapshot = {
  receiptNumber: string;
  total: number;
  tax: number;
};

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : null;
}

function readNumber(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : Number.NaN;
}

function readString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

/** Pull Beleg number, gross, and tax from a POS payment JSON body. */
export function readPaymentSnapshot(body: unknown): PaymentSnapshot | null {
  const root = asRecord(body);
  if (!root) return null;
  const payment = asRecord(root.payment) ?? asRecord(root.Payment);
  const tse = asRecord(root.tse) ?? asRecord(root.Tse);
  const receiptNumber =
    readString(payment?.receiptNumber) ||
    readString(payment?.ReceiptNumber) ||
    readString(tse?.receiptNumber) ||
    readString(tse?.ReceiptNumber) ||
    readString(root.receiptNumber);
  if (!receiptNumber) return null;
  const total = readNumber(payment?.totalAmount ?? payment?.TotalAmount ?? root.totalAmount);
  const tax = readNumber(payment?.taxAmount ?? payment?.TaxAmount ?? root.taxAmount);
  return { receiptNumber, total, tax };
}

export async function signInCashier(page: Page, slug = TABLES_TENANT): Promise<void> {
  const account = users[slug];
  if (!account?.username || !account.password) {
    throw new Error(`Missing fixture credentials for ${slug}`);
  }
  await openLogin(page, slug);
  await login(page, account.username, account.password);
  await openCashRegister(page);
}

export async function selectTable(page: Page, tableNumber: number): Promise<void> {
  const selected = page.getByRole('button', { name: `Tisch ${tableNumber}, ausgewählt` });
  if (await selected.isVisible().catch(() => false)) return;
  await page.getByRole('button', { name: `Tisch ${tableNumber} wählen` }).click();
  await expect(selected).toBeVisible();
}

export function payButton(page: Page) {
  return page.getByRole('button', { name: /^Bezahlen/ });
}

export async function emptyCart(page: Page): Promise<void> {
  const clear = page.getByRole('button', { name: 'Warenkorb leeren' });
  if (!(await clear.isVisible().catch(() => false))) return;
  await clear.evaluate((element) => {
    (element as HTMLElement).click();
  });
  await expect(payButton(page)).toBeDisabled({ timeout: 20_000 });
}

export async function addNamedProduct(page: Page, name: string): Promise<void> {
  const product = page.getByText(name, { exact: true }).filter({ visible: true }).first();
  await expect(product).toBeVisible({ timeout: 20_000 });
  await product.click();
}

async function dismissDevToast(page: Page): Promise<void> {
  await page.evaluate(() => document.getElementById('error-toast')?.remove());
}

export function watchPayments(page: Page): PaymentSnapshot[] {
  const snapshots: PaymentSnapshot[] = [];
  page.on('response', (response: Response) => {
    if (!response.ok() || response.request().method() !== 'POST') return;
    if (!response.url().includes('/pos/payment')) return;
    void response
      .json()
      .then((body) => {
        const snapshot = readPaymentSnapshot(body);
        if (snapshot) snapshots.push(snapshot);
      })
      .catch(() => undefined);
  });
  return snapshots;
}

/** Cash dialog: pick Bar, tender enough cash, then submit. */
export async function payCash(page: Page): Promise<void> {
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

/** RN Pressables often have no button role and sit below the modal fold. */
export async function pressText(page: Page, label: string): Promise<void> {
  const target = page.getByText(label, { exact: true }).last();
  await expect(target).toBeAttached();
  await target.evaluate((element) => {
    const host = (element.parentElement as HTMLElement | null) ?? (element as HTMLElement);
    host.scrollIntoView({ block: 'center', inline: 'nearest' });
    host.click();
  });
}

export async function finishPaymentSuccess(page: Page): Promise<void> {
  await expect(page.getByText('Zahlung erfolgreich', { exact: true })).toBeVisible({
    timeout: 45_000,
  });
  const done = page.getByRole('button', { name: 'Fertig', exact: true });
  // The success receipt can push Fertig below the modal fold. A normal click
  // then waits forever because the RN scroll view does not move it into view.
  await done.evaluate((element) => {
    element.scrollIntoView({ block: 'center', inline: 'nearest' });
    (element as HTMLElement).click();
  });
  await expect(page.getByText('Zahlung erfolgreich', { exact: true })).toBeHidden({
    timeout: 20_000,
  });
}
