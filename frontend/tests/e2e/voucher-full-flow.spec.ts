import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  finishPaymentSuccess,
  payButton,
  selectTable,
  signInCashier,
} from '../../e2e/helpers/sale';

const API = process.env.E2E_API_URL ?? 'http://127.0.0.1:5184';

test.describe('Voucher payment', () => {
  test.describe.configure({ timeout: 180_000 });

  test('applies a voucher, pays the remainder in cash, and keeps the leftover balance', async ({
    page,
  }) => {
    await signInCashier(page);
    const token = await page.evaluate(() => window.localStorage.getItem('token'));
    const tenant = await page.evaluate(() => window.localStorage.getItem('dev_tenant_id'));
    expect(token).toBeTruthy();
    const headers = {
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': tenant || 'dev-gastronomy-tables',
    };

    const now = new Date();
    const until = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);
    const issued = await page.request.post(`${API}/api/vouchers/issue`, {
      headers,
      data: {
        amount: 5,
        currency: 'EUR',
        validFrom: now.toISOString(),
        validUntil: until.toISOString(),
      },
    });
    const issuedBody = await issued.text();
    expect(issued.ok(), issuedBody).toBeTruthy();
    const voucher = JSON.parse(issuedBody) as { fullCode?: string };
    expect(voucher.fullCode).toBeTruthy();

    await selectTable(page, 4);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    const before = (await payButton(page).innerText()).replace(/\s+/g, ' ');
    await payButton(page).click();

    await page.getByRole('button', { name: /^Gutschein einlösen/ }).click();
    await page.getByRole('textbox', { name: 'Gutscheincode' }).fill(voucher.fullCode ?? '');
    await page.getByRole('button', { name: 'Gutschein prüfen' }).click();
    await expect(page.getByText(/Verfügbares Guthaben/)).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText(/5,00/).first()).toBeVisible();

    await page.getByRole('textbox', { name: 'Einzulösender Betrag' }).fill('1.00');
    const restRow = page.getByText('Restbetrag', { exact: true }).locator('..');
    await expect(restRow).toBeVisible();
    await expect(restRow).toContainText(/€/);
    await expect(restRow).not.toContainText(before);

    await page.getByRole('button', { name: /^Bar\b/ }).click();
    await page.getByRole('button', { name: /^€ \d/ }).last().click();
    const confirm = page.getByRole('button', { name: /^Zahlen / });
    await expect(confirm).toBeEnabled({ timeout: 20_000 });
    await confirm.click();
    await finishPaymentSuccess(page);

    const checked = await page.request.post(`${API}/api/pos/vouchers/validate`, {
      headers,
      data: { voucherCode: voucher.fullCode, amount: 1 },
    });
    const checkedBody = await checked.text();
    expect(checked.ok(), checkedBody).toBeTruthy();
    const balance = JSON.parse(checkedBody) as {
      remainingAmount?: number;
      data?: { remainingAmount?: number };
    };
    const remaining = balance.remainingAmount ?? balance.data?.remainingAmount;
    expect(remaining).toBeCloseTo(4, 2);
  });
});
