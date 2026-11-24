import { expect, test } from '@playwright/test';

import {
  addNamedProduct,
  emptyCart,
  payButton,
  selectTable,
  signInCashier,
} from '../../e2e/helpers/sale';

const CUSTOMER_SHEET_LABEL = 'Kunden-Nr.';
const EMPTY_CART_TOAST = 'Warenkorb ist leer. Bitte zuerst Artikel hinzufügen.';

test.describe('POS keyboard shortcuts (web)', () => {
  test.describe.configure({ timeout: 180_000 });

  test('F1, ? and Esc drive the cheat sheet, F2 opens the customer sheet', async ({ page }) => {
    await signInCashier(page);
    const help = page.getByTestId('pos-shortcuts-help');

    await page.keyboard.press('F1');
    await expect(help).toBeVisible();
    await expect(help.getByText('Bezahlen', { exact: true })).toBeVisible();
    await expect(help.getByText('Dialog schließen', { exact: true })).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(help).toBeHidden();

    await page.keyboard.press('?');
    await expect(help).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(help).toBeHidden();

    await page.getByTestId('pos-shortcuts-open').click();
    await expect(help).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(help).toBeHidden();

    await page.keyboard.press('F2');
    await expect(page.getByText(CUSTOMER_SHEET_LABEL)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.getByText(CUSTOMER_SHEET_LABEL)).toBeHidden();
  });

  test('F6 pays only with items in the cart and never under a sheet', async ({ page }) => {
    await signInCashier(page);
    await selectTable(page, 1);
    await emptyCart(page);
    const cashOption = page.getByRole('button', { name: /^Bar\b/ });

    await page.keyboard.press('F6');
    await expect(page.getByText(EMPTY_CART_TOAST)).toBeVisible();
    await expect(cashOption).toHaveCount(0);

    await addNamedProduct(page, 'Suppe');
    await expect(payButton(page)).toBeEnabled({ timeout: 20_000 });

    // While the customer sheet is open only Esc is active, so F6 cannot start a payment.
    await page.keyboard.press('F2');
    await expect(page.getByText(CUSTOMER_SHEET_LABEL)).toBeVisible();
    await page.keyboard.press('F6');
    await expect(cashOption).toHaveCount(0);
    await page.keyboard.press('Escape');
    await expect(page.getByText(CUSTOMER_SHEET_LABEL)).toBeHidden();

    await page.keyboard.press('F6');
    await expect(cashOption).toBeVisible({ timeout: 20_000 });
  });

  test('F5 asks before clearing the cart and does not reload the page', async ({ page }) => {
    const dialogs: string[] = [];
    page.on('dialog', (dialog) => {
      dialogs.push(dialog.message());
    });

    await signInCashier(page);
    await selectTable(page, 1);
    await emptyCart(page);
    await addNamedProduct(page, 'Suppe');
    await expect(payButton(page)).toBeEnabled({ timeout: 20_000 });
    await page.evaluate(() => {
      (window as unknown as { __shortcutMarker?: boolean }).__shortcutMarker = true;
    });

    // The shared login helper accepts every dialog, so the confirm is accepted here.
    await page.keyboard.press('F5');

    await expect.poll(() => dialogs.some((text) => text.includes('Warenkorb leeren?'))).toBe(true);
    await expect(payButton(page)).toBeDisabled({ timeout: 20_000 });
    const markerSurvived = await page.evaluate(
      () => (window as unknown as { __shortcutMarker?: boolean }).__shortcutMarker === true
    );
    expect(markerSurvived).toBe(true);
  });
});
