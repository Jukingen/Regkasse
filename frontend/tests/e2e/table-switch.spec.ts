import { expect, test } from '@playwright/test';

import { addNamedProduct, emptyCart, selectTable, signInCashier } from '../../e2e/helpers/sale';

test.describe('Table carts stay separate', () => {
  test.describe.configure({ timeout: 180_000 });

  test('products added on table 1 stay off table 2', async ({ page }) => {
    await signInCashier(page);
    await selectTable(page, 1);
    await emptyCart(page);
    await addNamedProduct(page, 'Tagesmenü');
    await expect(page.getByText(/Tisch 1 · [1-9]/)).toBeVisible();

    await selectTable(page, 2);
    await emptyCart(page);
    await expect(page.getByText(/Tisch 2 · 0 Artikel/)).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Tagesmenü', { exact: true })).toHaveCount(
      0
    );

    await addNamedProduct(page, 'Suppe');
    await expect(page.getByTestId('pos-order-panel').getByText('Suppe', { exact: true })).toBeVisible();

    await selectTable(page, 1);
    await expect(page.getByTestId('pos-order-panel').getByText('Tagesmenü', { exact: true })).toBeVisible();
    await expect(page.getByTestId('pos-order-panel').getByText('Suppe', { exact: true })).toHaveCount(0);

    await emptyCart(page);
    await selectTable(page, 2);
    await emptyCart(page);
  });
});
