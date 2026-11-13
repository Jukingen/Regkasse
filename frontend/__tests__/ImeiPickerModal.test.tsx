import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';

import { ImeiPickerModal } from '../components/ImeiPickerModal';
import { changeLanguage } from '../i18n';
import { productRequiresImeiPicker } from '../services/api/imeiService';

const mockList = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockImeiFeatures = { imeiTracking: true };

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => ({
    profileId: 'handy-shop',
    posLayout: 'standard',
    posFeatures: mockImeiFeatures,
    requiredFields: { customer: [], product: [] },
    optionalFields: { customer: [], product: [] },
  }),
}));

jest.mock('../services/api/imeiService', () => ({
  listProductImeis: (...args: unknown[]) => mockList(...args),
  productRequiresImeiPicker: (product: { imeiTracked?: boolean | null }) =>
    product.imeiTracked === true,
}));

jest.mock('../components/BarcodeScannerModal', () => ({
  BarcodeScannerModal: () => null,
}));

describe('ImeiPickerModal', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockImeiFeatures.imeiTracking = true;
    mockList.mockReset();
    mockList.mockResolvedValue([
      {
        id: 'imei-1',
        productId: 'prod-1',
        imei: '490154203237518',
        status: 'InStock',
        warrantyMonths: 12,
        createdAtUtc: '2026-09-30T00:00:00Z',
      },
    ]);
  });

  it('shows the IMEI picker for tracked products', async () => {
    expect(productRequiresImeiPicker({ imeiTracked: true })).toBe(true);
    expect(productRequiresImeiPicker({ imeiTracked: false })).toBe(false);

    const cashRegister = fs.readFileSync(
      path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
      'utf8'
    );
    expect(cashRegister).toContain('productRequiresImeiPicker(product)');
    expect(cashRegister).toContain('<ImeiPickerModal');

    const onSelect = jest.fn();
    await render(
      <ImeiPickerModal
        visible
        productId="prod-1"
        productName="Pixel"
        onClose={() => undefined}
        onSelect={onSelect}
      />
    );

    expect(await screen.findByLabelText('IMEI wählen')).toBeTruthy();
    expect(await screen.findByLabelText('IMEI scannen')).toBeTruthy();
    const row = await screen.findByLabelText('IMEI 490154203237518 wählen');
    await fireEvent.press(row);
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith('490154203237518'));
  });

  it('hides the picker when imeiTracking is off', async () => {
    mockImeiFeatures.imeiTracking = false;
    await render(
      <ImeiPickerModal
        visible
        productId="prod-1"
        productName="Pixel"
        onClose={() => undefined}
        onSelect={() => undefined}
      />
    );

    expect(screen.queryByLabelText('IMEI wählen')).toBeNull();
    expect(mockList).not.toHaveBeenCalled();
  });
});
