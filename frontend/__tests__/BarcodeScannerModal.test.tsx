import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { BarcodeScannerModal } from '../components/BarcodeScannerModal';
import { POS_PRODUCT_BARCODE_TYPES, POS_RETAIL_BARCODE_TYPES } from '../constants/posCameraScan';
import { changeLanguage } from '../i18n';

const captured = {
  types: [] as string[],
};

jest.mock('../hooks/usePosCameraPermission', () => ({
  usePosCameraPermission: () => ({
    ui: 'granted',
    requestPermission: jest.fn(),
    openSettings: jest.fn(),
  }),
}));

jest.mock('../components/camera/PosCameraView', () => {
  const React = require('react');
  const { Pressable, Text } = require('react-native');
  return {
    PosCameraView: (props: {
      barcodeScannerSettings?: { barcodeTypes?: string[] };
      onBarcodeScanned?: (event: { data: string }) => void;
    }) => {
      captured.types = props.barcodeScannerSettings?.barcodeTypes ?? [];
      return React.createElement(
        Pressable,
        {
          accessibilityLabel: 'fire-scan',
          onPress: () => props.onBarcodeScanned?.({ data: '  RAW-99  ' }),
        },
        React.createElement(Text, null, 'camera')
      );
    },
  };
});

jest.mock('@expo/vector-icons', () => {
  const React = require('react');
  const { Text } = require('react-native');
  return {
    Ionicons: (props: { name?: string }) => React.createElement(Text, null, props.name ?? ''),
  };
});

describe('BarcodeScannerModal mode', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    captured.types = [];
  });

  it('returns the trimmed ticket code in ticket mode', async () => {
    const onScan = jest.fn();
    await render(
      <BarcodeScannerModal
        visible
        mode="ticket"
        title="Ticket"
        onClose={jest.fn()}
        onScan={onScan}
      />
    );

    fireEvent.press(screen.getByLabelText('fire-scan'));
    expect(onScan).toHaveBeenCalledWith('RAW-99');
    expect(captured.types).toEqual([...POS_PRODUCT_BARCODE_TYPES]);
  });

  it('returns the raw barcode in product mode', async () => {
    const onScan = jest.fn();
    await render(
      <BarcodeScannerModal
        visible
        mode="product"
        title="Barcode"
        onClose={jest.fn()}
        onScan={onScan}
      />
    );

    fireEvent.press(screen.getByLabelText('fire-scan'));
    expect(onScan).toHaveBeenCalledWith('RAW-99');
    expect(captured.types).toEqual([...POS_RETAIL_BARCODE_TYPES]);
  });
});
