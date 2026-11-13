import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

jest.mock('../src/components/common/WaveLoader', () => {
  const React = require('react') as typeof import('react');
  const { View } = require('react-native') as typeof import('react-native');
  return {
    WaveLoader: () => React.createElement(View, { testID: 'wave-loader' }),
  };
});

import { CartSummary } from '../components/CartSummary';
import { changeLanguage } from '../i18n';

describe('CartSummary Bezahlen', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('disables Bezahlen when cartItems.length is 0', async () => {
    const onPayment = jest.fn();
    await render(
      <CartSummary cart={{ items: [] }} loading={false} error={null} onPayment={onPayment} />
    );

    const button = await screen.findByRole('button', { name: /Bezahlen/ });
    expect(button.props.accessibilityState?.disabled).toBe(true);
  });

  it('leaves Bezahlen enabled when the cart has items', async () => {
    const onPayment = jest.fn();
    await render(
      <CartSummary
        cart={{
          items: [{ qty: 1, productId: 'p1' }],
          subtotalGross: 10,
          includedTaxTotal: 1.67,
          grandTotalGross: 10,
        }}
        loading={false}
        error={null}
        onPayment={onPayment}
      />
    );

    const button = await screen.findByRole('button', { name: /Bezahlen/ });
    expect(button.props.accessibilityState?.disabled).toBe(false);
  });
});
