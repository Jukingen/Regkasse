import { expect, test, type Page } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  finishPaymentSuccess,
  payCash,
  pressText,
  selectTable,
  signInCashier,
  watchPayments,
} from '../../e2e/helpers/sale';

const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

type CatalogProduct = { id: string; name: string; price: number; stock: number };

async function authHeaders(page: Page): Promise<Record<string, string>> {
  const token = await page.evaluate(() => window.localStorage.getItem('token'));
  const tenant = await page.evaluate(() => window.localStorage.getItem('dev_tenant_id'));
  return {
    Authorization: `Bearer ${token}`,
    'X-Tenant-Id': tenant || 'dev-gastronomy-tables',
  };
}

function collectProducts(value: unknown, found: Map<string, CatalogProduct>): void {
  if (Array.isArray(value)) {
    for (const item of value) collectProducts(item, found);
    return;
  }
  if (!value || typeof value !== 'object') return;
  const record = value as Record<string, unknown>;
  if (
    typeof record.id === 'string' &&
    typeof record.name === 'string' &&
    typeof record.price === 'number' &&
    typeof record.stockQuantity === 'number'
  ) {
    found.set(record.id, {
      id: record.id,
      name: record.name,
      price: record.price,
      stock: record.stockQuantity,
    });
  }
  for (const child of Object.values(record)) collectProducts(child, found);
}

function collectInvoices(value: unknown, found: Array<Record<string, unknown>>): void {
  if (Array.isArray(value)) {
    for (const item of value) {
      if (item && typeof item === 'object' && ('documentType' in item || 'DocumentType' in item)) {
        found.push(item as Record<string, unknown>);
      } else {
        collectInvoices(item, found);
      }
    }
    return;
  }
  if (!value || typeof value !== 'object') return;
  for (const child of Object.values(value as Record<string, unknown>)) collectInvoices(child, found);
}

test.describe('Partial refund of 10 EUR', () => {
  test.describe.configure({ timeout: 180_000 });

  test('refunds 10 EUR of a sale of at least 20 EUR and keeps the credit note and stock', async ({
    page,
  }) => {
    const payments = watchPayments(page);
    await signInCashier(page);
    const headers = await authHeaders(page);
    const catalogResponse = await page.request.get(`${API}/api/pos/catalog`, { headers });
    const catalogBody = await catalogResponse.text();
    expect(catalogResponse.ok(), catalogBody).toBeTruthy();
    const products = new Map<string, CatalogProduct>();
    collectProducts(JSON.parse(catalogBody) as unknown, products);
    const product = [...products.values()].find((item) => item.name === 'Tagesmenü' && item.price > 0);
    expect(product, 'Tagesmenü is missing from the catalog').toBeTruthy();

    await selectTable(page, 3);
    await emptyCart(page);
    // The cart line repeats the product name, so each tap uses the catalog tile.
    for (let index = 0; index < 3; index += 1) {
      const menu = page.getByText('Tagesmenü', { exact: true }).filter({ visible: true }).last();
      await expect(menu).toBeVisible({ timeout: 20_000 });
      await menu.click();
    }
    await payCash(page);
    await finishPaymentSuccess(page);
    await expect.poll(() => payments.length).toBeGreaterThan(0);

    const sale = payments[0];
    expect(sale.total).toBeGreaterThanOrEqual(20);
    expect(sale.tax).toBeGreaterThan(0);
    const refundGross = 10;

    await addNamedProduct(page, 'Mineralwasser');
    await page.getByRole('button', { name: /^Bezahlen/ }).click();
    await page.getByRole('button', { name: 'Storno / Rückerstattung' }).click();
    await page.getByRole('button', { name: 'TEILRÜCKERSTATTUNG' }).click();
    await page.getByPlaceholder(/AT-KASSE/).fill(sale.receiptNumber);
    await pressText(page, 'Positionen laden');
    const amount = page.getByRole('textbox', { name: '0,00' });
    await expect(amount).toHaveValue(/\d/, { timeout: 20_000 });
    await amount.fill(refundGross.toFixed(2));
    await pressText(page, 'Erstattung buchen');

    await expect.poll(() => payments.length).toBeGreaterThan(1);
    const refund = payments.at(-1);
    expect(refund?.receiptNumber).toMatch(/^AT-/);
    expect(refund?.receiptNumber).not.toBe(sale.receiptNumber);
    expect(refund?.total).toBeCloseTo(-refundGross, 2);
    const ratio = refundGross / sale.total;
    expect(Math.abs(refund?.tax ?? Number.NaN)).toBeCloseTo(Math.abs(sale.tax) * ratio, 2);

    const invoicesResponse = await page.request.get(`${API}/api/Invoice?pageSize=50`, { headers });
    const invoicesBody = await invoicesResponse.text();
    expect(invoicesResponse.ok(), invoicesBody).toBeTruthy();
    const invoices: Array<Record<string, unknown>> = [];
    collectInvoices(JSON.parse(invoicesBody) as unknown, invoices);
    const creditNote = invoices.find((row) => {
      const type = row.documentType ?? row.DocumentType;
      const total = row.totalAmount ?? row.TotalAmount;
      const isCredit = type === 'CreditNote' || type === 1;
      return isCredit && typeof total === 'number' && total < 0;
    });
    expect(creditNote, 'expected a negative credit note').toBeTruthy();

    const afterCatalog = await page.request.get(`${API}/api/pos/catalog`, { headers });
    expect(afterCatalog.ok()).toBeTruthy();
    const after = new Map<string, CatalogProduct>();
    collectProducts(await afterCatalog.json(), after);
    expect(after.get(product?.id ?? '')?.stock).toBe(product?.stock);
  });
});
