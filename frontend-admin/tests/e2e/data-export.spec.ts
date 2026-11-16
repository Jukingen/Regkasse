import { readFileSync } from 'node:fs';

import { expect, test } from '@playwright/test';

import { expectAuthenticatedShell, loginAsAdmin, preparePage } from './helpers/auth';
import { E2E_TENANT_ADMIN_LOGIN } from './helpers/apiMocks';

/** GET /api/admin/export/products — TenantDataExportService.ProductHeaders. */
const TENANT_EXPORT_HEADERS = [
  'Id',
  'TenantId',
  'Name',
  'Price',
  'TaxRate',
  'Barcode',
  'Category',
  'StockQuantity',
  'IsActive',
];

/** GET /api/admin/products/export — ProductExportService catalog CSV. */
const CATALOG_EXPORT_HEADERS = [
  'Id',
  'Name',
  'NameDe',
  'NameEn',
  'NameTr',
  'Price',
  'Cost',
  'TaxType',
  'TaxRate',
  'Barcode',
  'Category',
  'CategoryId',
  'Unit',
  'StockQuantity',
  'MinStockLevel',
  'MaxStockLevel',
  'IsActive',
  'IsTaxable',
  'IsFiscalCompliant',
  'FiscalCategoryCode',
  'RksvProductType',
  'IsSellableAddOn',
  'CreatedAt',
  'UpdatedAt',
];

function csvFor(headers: string[]): string {
  return [
    headers.join(','),
    headers.map((column) => (column === 'Name' ? 'Espresso' : '')).join(','),
  ].join('\n');
}

test('tenant admin downloads a product CSV with the canonical headers', async ({ page }) => {
  await preparePage(page);

  await page.route('**/api/admin/products**', async (route) => {
    if (route.request().method() !== 'GET') {
      await route.fallback();
      return;
    }
    const url = new URL(route.request().url());
    if (url.pathname !== '/api/admin/products') {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: '11111111-1111-4111-8111-111111111111',
            name: 'Espresso',
            price: 2.5,
            taxRate: 20,
            isActive: true,
          },
        ],
        pagination: { pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
      }),
    });
  });

  const fulfillCsv = async (
    route: import('@playwright/test').Route,
    headers: string[]
  ) => {
    const url = new URL(route.request().url());
    expect(url.searchParams.get('format')).toBe('csv');
    await route.fulfill({
      status: 200,
      contentType: 'text/csv; charset=utf-8',
      headers: {
        'content-disposition': 'attachment; filename="products.csv"',
      },
      body: csvFor(headers),
    });
  };

  await page.route('**/api/admin/export/products**', (route) =>
    fulfillCsv(route, TENANT_EXPORT_HEADERS)
  );
  await page.route('**/api/admin/products/export**', (route) =>
    fulfillCsv(route, CATALOG_EXPORT_HEADERS)
  );

  await loginAsAdmin(page, { loginIdentifier: E2E_TENANT_ADMIN_LOGIN });
  await expectAuthenticatedShell(page);
  await page.goto('/products', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Produkte' })).toBeVisible();

  const tenantExport = page.getByTestId('tenant-export-products');
  const catalogExport = page.getByRole('button', { name: 'CSV exportieren' });
  const downloadPromise = page.waitForEvent('download');
  const expectedHeaders = (await tenantExport.count())
    ? TENANT_EXPORT_HEADERS
    : CATALOG_EXPORT_HEADERS;

  if (expectedHeaders === TENANT_EXPORT_HEADERS) {
    await tenantExport.click();
    await page.getByRole('menuitem', { name: 'CSV' }).click();
  } else {
    await catalogExport.click();
    await page.getByRole('button', { name: 'Herunterladen' }).click();
  }

  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/\.csv$/i);
  const filePath = await download.path();
  expect(filePath).toBeTruthy();
  const text = readFileSync(filePath as string, 'utf8');
  const header = (text.replace(/^\uFEFF/, '').split(/\r?\n/)[0] ?? '').split(',');
  expect(header).toEqual(expectedHeaders);
  expect(text).toContain('Espresso');
});
