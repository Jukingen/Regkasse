import { expect, test, type Page } from '@playwright/test';

import { expectAuthenticatedShell, injectAdminSession, loginAsAdmin, preparePage } from './helpers/auth';
import { isLiveE2E } from './helpers/env';

const MAPPINGS_PATH = '/api/admin/online-order-integrations/product-mappings';
const PRODUCTS_PATH = '/api/admin/products';
const PRODUCT_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const EXTERNAL_ID = 'plu-e2e-pizza';

type MappingRow = {
  id: string;
  provider: string;
  externalProductId: string;
  productId: string;
  productName: string;
  createdAt: string;
  updatedAt: string | null;
};

async function installProductMappingMocks(page: Page) {
  const store: MappingRow[] = [];

  await page.route(`**${PRODUCTS_PATH}*`, async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [{ id: PRODUCT_ID, name: 'Pizza', isActive: true }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      }),
    });
  });

  await page.route(`**${MAPPINGS_PATH}*`, async (route) => {
    const request = route.request();
    if (request.method() === 'GET') {
      const provider = new URL(request.url()).searchParams.get('provider')?.trim().toLowerCase();
      const items = provider
        ? store.filter((row) => row.provider === provider)
        : store;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items }),
      });
      return;
    }

    if (request.method() === 'PUT') {
      const body = request.postDataJSON() as {
        provider?: string;
        externalProductId?: string;
        productId?: string;
      };
      const row: MappingRow = {
        id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
        provider: (body.provider ?? 'deliverect').trim().toLowerCase(),
        externalProductId: (body.externalProductId ?? '').trim(),
        productId: body.productId ?? PRODUCT_ID,
        productName: 'Pizza',
        createdAt: '2026-10-28T22:00:00.000Z',
        updatedAt: '2026-10-28T22:00:00.000Z',
      };
      const existing = store.findIndex(
        (item) =>
          item.provider === row.provider && item.externalProductId === row.externalProductId
      );
      if (existing >= 0) store[existing] = row;
      else store.unshift(row);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: row.id }),
      });
      return;
    }

    await route.fallback();
  });
}

test.describe('Marketplace product mapping', () => {
  test('saves a mapping and shows it after reload', async ({ page }) => {
    await preparePage(page);
    await installProductMappingMocks(page);
    if (!isLiveE2E()) {
      await injectAdminSession(page);
    } else {
      await loginAsAdmin(page);
    }
    await page.goto('/admin/online-order-integrations', { waitUntil: 'domcontentloaded' });
    await expectAuthenticatedShell(page);

    await expect(
      page.getByRole('heading', { name: 'Marktplatz-Produktzuordnung' })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole('table')).toBeVisible();

    await page.getByRole('textbox', { name: 'Externe Produktnummer' }).fill(EXTERNAL_ID);
    await page.getByRole('combobox', { name: 'Katalogprodukt' }).click();
    await page.locator('.ant-select-item-option').filter({ hasText: 'Pizza' }).click();
    await page.getByRole('button', { name: 'Zuordnung speichern' }).click();

    await expect(page.getByRole('table').getByText(EXTERNAL_ID)).toBeVisible({ timeout: 15_000 });

    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(
      page.getByRole('heading', { name: 'Marktplatz-Produktzuordnung' })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByRole('table').getByText(EXTERNAL_ID)).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Pizza', exact: true })).toBeVisible();
  });

  test('saves a wolt provider mapping', async ({ page }) => {
    await preparePage(page);
    await installProductMappingMocks(page);
    if (!isLiveE2E()) {
      await injectAdminSession(page);
    } else {
      await loginAsAdmin(page);
    }
    await page.goto('/admin/online-order-integrations', { waitUntil: 'domcontentloaded' });
    await expectAuthenticatedShell(page);

    await expect(
      page.getByRole('heading', { name: 'Marktplatz-Produktzuordnung' })
    ).toBeVisible({ timeout: 20_000 });
    await page.getByRole('textbox', { name: 'Anbieter' }).fill('wolt');
    await page.getByRole('textbox', { name: 'Externe Produktnummer' }).fill('wolt-sku-1');
    await page.getByRole('combobox', { name: 'Katalogprodukt' }).click();
    await page.locator('.ant-select-item-option').filter({ hasText: 'Pizza' }).click();
    await page.getByRole('button', { name: 'Zuordnung speichern' }).click();

    await expect(page.getByRole('table').getByText('wolt-sku-1')).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole('cell', { name: 'wolt', exact: true })).toBeVisible();
  });
});
