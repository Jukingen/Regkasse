import { expect, test } from '@playwright/test';

import { addNamedProduct, emptyCart, selectTable, signInCashier } from '../../e2e/helpers/sale';

test.describe('Kitchen display', () => {
  test.describe.configure({ timeout: 180_000 });

  test('sends a table order and shows it on the kitchen display', async ({ page }) => {
    await signInCashier(page);
    await selectTable(page, 8);
    await emptyCart(page);
    await addNamedProduct(page, 'Suppe');

    await page.getByRole('button', { name: 'An die Küche senden' }).click();
    await expect(page.getByText('Bestellung an die Küche gesendet.')).toBeVisible({
      timeout: 20_000,
    });

    await page.getByRole('tab', { name: 'Küche' }).click();
    await expect(page.getByTestId('kds-screen')).toBeVisible({ timeout: 20_000 });
    await expect(page.getByTestId('kds-column-Pending').getByText(/Suppe/).first()).toBeVisible({
      timeout: 20_000,
    });
  });
});
