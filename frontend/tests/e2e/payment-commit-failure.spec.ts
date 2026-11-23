import { expect, test } from '@playwright/test';

import { addNamedProduct, emptyCart, payCash, selectTable, signInCashier } from '../../e2e/helpers/sale';

test.describe('Payment commit failure', () => {
  test.describe.configure({ timeout: 180_000 });

  test('shows the payment error when the create call fails', async ({ page }) => {
    let intercepted = 0;
    await page.route('**/pos/payment*', async (route) => {
      const url = route.request().url();
      const isCreate = route.request().method() === 'POST' && /\/pos\/payment(\?|$)/.test(url);
      if (!isCreate) {
        await route.continue();
        return;
      }
      intercepted += 1;
      await route.fulfill({
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'An error occurred while creating payment',
        }),
      });
    });

    await signInCashier(page);
    await selectTable(page, 3);
    await emptyCart(page);
    await addNamedProduct(page, 'Mineralwasser');
    await payCash(page);

    await expect(page.getByText('Fehler bei der Zahlung. Bitte erneut versuchen.')).toBeVisible({
      timeout: 20_000,
    });
    expect(intercepted).toBeGreaterThan(0);
    await expect(page.getByText('Zahlung erfolgreich')).not.toBeVisible();
  });
});
