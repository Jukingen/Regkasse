import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  pressText,
  selectTable,
  signInCashier,
} from '../../e2e/helpers/sale';

const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

test.describe('Benefit daily limit', () => {
  test.describe.configure({ timeout: 180_000 });

  test('opens the benefit preview and does not apply a daily-limit block to a new customer', async ({
    page,
  }) => {
    await signInCashier(page);
    const token = await page.evaluate(() => window.localStorage.getItem('token'));
    const tenant = await page.evaluate(() => window.localStorage.getItem('dev_tenant_id'));
    const headers = {
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': tenant || 'dev-gastronomy-tables',
    };
    const name = `E2E Limit ${Date.now()}`;
    const created = await page.request.post(`${API}/api/pos/customers`, {
      headers,
      data: { name, email: `e2e-limit-${Date.now()}@example.com` },
    });
    const createdBody = await created.text();
    expect(created.ok(), createdBody).toBeTruthy();

    await selectTable(page, 5);
    await emptyCart(page);
    await pressText(page, 'Setzen');
    await pressText(page, 'Aus Liste wählen');
    await page.getByPlaceholder('Name oder Nr. filtern').fill(name);
    await page.getByText(name, { exact: true }).click();
    await expect(page.getByText(name, { exact: true }).first()).toBeVisible();

    await addNamedProduct(page, 'Mineralwasser');
    const preview = page.waitForResponse(
      (response) =>
        response.url().includes('benefit-eligibility-preview') &&
        response.request().method() === 'POST'
    );
    await page.getByRole('button', { name: /^Bezahlen/ }).click();
    const response = await preview;
    const body = await response.text();
    expect(response.ok(), body).toBeTruthy();
    expect(body).not.toContain('DailyLimitReached');
    expect(body).not.toContain('Tageslimit');

    await expect(page.getByText('Vorteile (Vorschau)')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Tageslimit erreicht')).toHaveCount(0);
  });
});
