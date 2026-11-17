import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  payButton,
  selectTable,
  signInCashier,
} from '../../e2e/helpers/sale';

const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

test.describe('Voucher redemption', () => {
  test.describe.configure({ timeout: 180_000 });

  test('applies a voucher and shows the reduced amount due', async ({ page }) => {
    await signInCashier(page);
    const token = await page.evaluate(() => window.localStorage.getItem('token'));
    const tenant = await page.evaluate(() => window.localStorage.getItem('dev_tenant_id'));
    expect(token).toBeTruthy();

    const now = new Date();
    const until = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
    const issued = await page.request.post(`${API}/api/vouchers/issue`, {
      headers: {
        Authorization: `Bearer ${token}`,
        'X-Tenant-Id': tenant || 'dev-gastronomy-tables',
      },
      data: {
        amount: 1,
        currency: 'EUR',
        validFrom: now.toISOString(),
        validUntil: until.toISOString(),
      },
    });
    expect(issued.ok(), await issued.text()).toBeTruthy();
    const voucher = (await issued.json()) as { fullCode?: string; amount?: number };
    expect(voucher.fullCode).toBeTruthy();

    await selectTable(page, 5);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    const before = (await payButton(page).innerText()).replace(/\s+/g, ' ');
    await payButton(page).click();

    await page.getByRole('button', { name: /^Gutschein einlösen/ }).click();
    await page.getByRole('textbox', { name: 'Gutscheincode' }).fill(voucher.fullCode ?? '');
    await page.getByRole('button', { name: 'Gutschein prüfen' }).click();
    await expect(page.getByText(/Verfügbares Guthaben/)).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/1,00/).first()).toBeVisible();

    await page.getByRole('textbox', { name: 'Einzulösender Betrag' }).fill('1.00');
    const restRow = page.getByText('Restbetrag', { exact: true }).locator('..');
    await expect(restRow).toBeVisible();
    await expect(restRow).toContainText(/€/);
    await expect(restRow).not.toContainText(before);
  });
});
