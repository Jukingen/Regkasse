import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { CartDisplay } from '../components/CartDisplay';
import { changeLanguage } from '../i18n';

// The real CartContext pulls in the API client, which needs an API base URL at import time.
jest.mock('../contexts/CartContext', () => ({
  getCartDisplayTotals: () => ({ itemCount: 0, subtotalGross: 0, includedTaxTotal: 0, grandTotalGross: 0 }),
}));

jest.mock('../src/components/common/WaveLoader', () => ({
  WaveLoader: () => null,
}));

const noop = async () => undefined;

describe('CartDisplay loading state', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('shows skeleton rows and a spinner with German text while the cart loads', async () => {
    await render(
      <CartDisplay
        cart={null}
        selectedTable={3}
        loading
        error={null}
        onQuantityUpdate={noop}
        onItemRemove={noop}
        onClearCart={noop}
      />
    );

    expect(screen.getByTestId('pos-cart-skeleton')).toBeTruthy();
    expect(screen.getByText('Warenkorb wird geladen…')).toBeTruthy();
    expect(screen.getByText('Tisch 3')).toBeTruthy();
  });

  it('drops the skeleton once the cart is loaded', async () => {
    await render(
      <CartDisplay
        cart={{ items: [] }}
        selectedTable={3}
        loading={false}
        error={null}
        onQuantityUpdate={noop}
        onItemRemove={noop}
        onClearCart={noop}
      />
    );

    expect(screen.queryByTestId('pos-cart-skeleton')).toBeNull();
    expect(screen.getByText('Leer')).toBeTruthy();
  });
});
