import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';

import { ProductBarcodeScan } from '../components/ProductBarcodeScan';
import { changeLanguage } from '../i18n';

const mockGetByBarcode = jest.fn<(barcode: string) => Promise<unknown>>();

jest.mock('../services/api/productService', () => ({
  getProductByBarcode: (barcode: string) => mockGetByBarcode(barcode),
}));

jest.mock('../components/BarcodeScannerModal', () => {
  const React = require('react');
  const { Pressable, Text } = require('react-native');
  return {
    BarcodeScannerModal: (props: {
      visible: boolean;
      mode?: string;
      onScan: (code: string) => void;
    }) =>
      React.createElement(
        Pressable,
        {
          accessibilityLabel: props.visible ? `scanner-${props.mode}` : 'scanner-closed',
          onPress: () => props.onScan('4001234567890'),
        },
        React.createElement(Text, null, props.visible ? props.mode : 'closed')
      ),
  };
});

describe('cash register barcode scan', () => {
  const onProduct = jest.fn();
  const onNotify = jest.fn();

  beforeEach(async () => {
    await changeLanguage('de');
    mockGetByBarcode.mockReset();
    onProduct.mockReset();
    onNotify.mockReset();
  });

  it('hides Scannen unless barcodeScan is enabled', async () => {
    await render(
      <ProductBarcodeScan
        enabled={false}
        scanBlocked={false}
        onProduct={onProduct}
        onNotify={onNotify}
      />
    );
    expect(screen.queryByLabelText('Scannen')).toBeNull();
  });

  it('opens the scanner in product mode and adds the product', async () => {
    mockGetByBarcode.mockResolvedValue({
      id: 'p1',
      name: 'Milch',
      price: 2,
      hasVariants: false,
    });

    await render(
      <ProductBarcodeScan
        enabled
        scanBlocked={false}
        onProduct={onProduct}
        onNotify={onNotify}
      />
    );

    fireEvent.press(screen.getByLabelText('Scannen'));
    fireEvent.press(await screen.findByLabelText('scanner-product'));

    await waitFor(() => {
      expect(mockGetByBarcode).toHaveBeenCalledWith('4001234567890');
      expect(onProduct).toHaveBeenCalledWith(expect.objectContaining({ id: 'p1', name: 'Milch' }));
    });
  });

  it('shows the German toast when the barcode is unknown', async () => {
    mockGetByBarcode.mockRejectedValue({
      response: { status: 404, data: { code: 'BARCODE_NOT_FOUND' } },
    });

    await render(
      <ProductBarcodeScan
        enabled
        scanBlocked={false}
        onProduct={onProduct}
        onNotify={onNotify}
      />
    );

    fireEvent.press(screen.getByLabelText('Scannen'));
    fireEvent.press(await screen.findByLabelText('scanner-product'));

    await waitFor(() => {
      expect(onNotify).toHaveBeenCalledWith(
        'error',
        'Kein Produkt mit diesem Barcode gefunden.'
      );
    });
    expect(onProduct).not.toHaveBeenCalled();
  });

  it('ignores a scan while the variant picker is open', async () => {
    await render(
      <ProductBarcodeScan enabled scanBlocked onProduct={onProduct} onNotify={onNotify} />
    );

    expect(screen.getByLabelText('Scannen').props.accessibilityState?.disabled).toBe(true);
  });

  it('wires the scan button to barcodeScan on the cash register', () => {
    const source = fs.readFileSync(
      path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
      'utf8'
    );
    expect(source).toContain('posFeatures.barcodeScan');
    expect(source).toContain('<ProductBarcodeScan');
    expect(source).toContain('scanBlocked={pendingVariantProduct != null}');
  });
});
