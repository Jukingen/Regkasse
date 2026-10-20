import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';

import { VariantPickerModal } from '../components/VariantPickerModal';
import { changeLanguage } from '../i18n';
import {
  productRequiresVariantPicker,
  variantCartLineLabel,
} from '../services/api/variantService';

const mockList = jest.fn<(...args: unknown[]) => Promise<unknown>>();

jest.mock('../services/api/variantService', () => ({
  listProductVariants: (...args: unknown[]) => mockList(...args),
  productRequiresVariantPicker: (product: { hasVariants?: boolean | null }) =>
    product.hasVariants === true,
  variantCartLineLabel: (productName: string, variantLabel: string) =>
    `${productName} — ${variantLabel}`,
}));

const red = {
  id: 'var-red',
  productId: 'prod-1',
  sku: 'SKU-ROT',
  variantLabel: 'Rot',
  priceDelta: 2.5,
  stock: 4,
  isActive: true,
};

const blue = {
  id: 'var-blue',
  productId: 'prod-1',
  sku: 'SKU-BLAU',
  variantLabel: 'Blau',
  priceDelta: 0,
  stock: 0,
  isActive: true,
};

describe('VariantPickerModal', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockList.mockReset();
    mockList.mockResolvedValue([red, blue]);
  });

  it('renders variant label, sku, and effective price', async () => {
    await render(
      <VariantPickerModal
        visible
        productId="prod-1"
        productName="T-Shirt"
        productPrice={10}
        onClose={() => undefined}
        onSelect={() => undefined}
      />
    );

    expect(await screen.findByLabelText('Variante wählen')).toBeTruthy();
    expect(await screen.findByText('Rot')).toBeTruthy();
    expect(screen.getByText('SKU-ROT')).toBeTruthy();
    expect(screen.getByText(/12,50/)).toBeTruthy();
  });

  it('disables out-of-stock variants', async () => {
    const onSelect = jest.fn();
    await render(
      <VariantPickerModal
        visible
        productId="prod-1"
        productName="T-Shirt"
        productPrice={10}
        onClose={() => undefined}
        onSelect={onSelect}
      />
    );

    const row = await screen.findByLabelText('Blau SKU-BLAU');
    expect(row.props.accessibilityState).toEqual({ disabled: true });
    expect(screen.getByText('Nicht auf Lager')).toBeTruthy();
    await fireEvent.press(row);
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('selects an in-stock variant and the cart line label includes the variant label', async () => {
    expect(productRequiresVariantPicker({ hasVariants: true })).toBe(true);
    expect(productRequiresVariantPicker({ hasVariants: false })).toBe(false);
    expect(variantCartLineLabel('T-Shirt', 'Rot')).toBe('T-Shirt — Rot');

    const cashRegister = fs.readFileSync(
      path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
      'utf8'
    );
    expect(cashRegister).toContain('productRequiresVariantPicker(product)');
    expect(cashRegister).toContain('<VariantPickerModal');
    expect(cashRegister).toContain('variantCartLineLabel');

    const onSelect = jest.fn();
    await render(
      <VariantPickerModal
        visible
        productId="prod-1"
        productName="T-Shirt"
        productPrice={10}
        onClose={() => undefined}
        onSelect={onSelect}
      />
    );

    const row = await screen.findByLabelText('Rot SKU-ROT');
    await fireEvent.press(row);
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ id: 'var-red', variantLabel: 'Rot' })));
  });

  it('shows the German sold-out message when every variant is out of stock', async () => {
    mockList.mockResolvedValue([{ ...red, stock: 0 }, blue]);
    const onSelect = jest.fn();
    await render(
      <VariantPickerModal
        visible
        productId="prod-1"
        productName="T-Shirt"
        productPrice={10}
        onClose={() => undefined}
        onSelect={onSelect}
      />
    );

    expect(await screen.findByText('Alle Varianten sind ausverkauft.')).toBeTruthy();
    await fireEvent.press(screen.getByLabelText('Rot SKU-ROT'));
    expect(onSelect).not.toHaveBeenCalled();
  });
});
