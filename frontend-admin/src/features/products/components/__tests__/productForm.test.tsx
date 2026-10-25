import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ProductForm from '@/features/products/components/ProductForm';
import { I18nProvider } from '@/i18n';

const messageError = vi.hoisted(() => vi.fn());

vi.mock('@/api/admin/vertical-profile', () => ({
  useAmbientVerticalProfile: () => ({ data: { posFeatures: {} } }),
}));

vi.mock('@/api/admin/staff', () => ({
  useAdminStaff: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/api/admin/product-imeis', () => ({
  useAdminProductImeis: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { error: messageError, success: vi.fn() },
    modal: {},
  }),
}));

vi.mock('@/hooks/useCurrentTaxRegulation', () => ({
  useCurrentTaxRegulation: () => ({ data: null }),
}));

vi.mock('@/hooks/useTaxGroups', () => ({
  useTaxGroups: () => ({
    data: [{ id: 'tg1', rate: 20, isDefault: true, isActive: true, name: 'Normal' }],
  }),
  resolveTaxGroupForProduct: () => ({ id: 'tg1', rate: 20 }),
}));

vi.mock('@/features/categories/hooks/useCategories', () => ({
  useCategories: () => ({
    useList: () => ({ data: [{ id: 'c1', name: 'Services' }] }),
  }),
}));

vi.mock('@/lib/api/modifierGroups', () => ({
  getModifierGroups: () => Promise.resolve([]),
  getProductModifierGroups: () => Promise.resolve([]),
}));

vi.mock('@/components/TaxSelect', () => ({
  TaxSelect: () => <div data-testid="tax-select" />,
}));

vi.mock('@/features/tax/components/PriceHistoryCard', () => ({
  PriceHistoryCard: () => null,
}));

vi.mock('@/features/tax/components/PriceChangeModal', () => ({
  PriceChangeModal: () => null,
}));

vi.mock('@/components/OptimizedImage', () => ({
  OptimizedImage: () => null,
}));

vi.mock('@/features/products/components/ExtraZutatenSection', () => ({
  default: () => null,
}));

vi.mock('@/features/products/components/ProductVariantsPanel', () => ({
  default: () => null,
}));

const initialValues = {
  id: 'p1',
  name: 'Milch',
  nameDe: 'Milch',
  price: 2,
  cost: 1,
  category: 'Services',
  categoryId: 'c1',
  taxGroupId: 'tg1',
  barcode: '',
  isActive: true,
  stockQuantity: 1,
  minStockLevel: 0,
  unit: 'Stk',
};

function renderForm(onSubmit: (values: { barcode?: string }) => Promise<void>) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <ProductForm
          visible
          isEditMode
          initialValues={initialValues as never}
          onCancel={() => undefined}
          onSubmit={onSubmit}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('ProductForm barcode', () => {
  beforeEach(() => {
    messageError.mockReset();
  });

  it('saves the barcode from the form', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    renderForm(onSubmit);

    const input = await screen.findByLabelText('Barcode');
    await user.clear(input);
    await user.type(input, '4001234567890');
    await user.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalled();
    });
    expect(onSubmit.mock.calls[0][0].barcode).toBe('4001234567890');
  });

  it('shows the translated toast when the barcode is already used', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockRejectedValue({
      response: {
        status: 409,
        data: {
          code: 'BARCODE_DUPLICATE',
          message: 'A product with this barcode already exists for this tenant.',
        },
      },
    });
    renderForm(onSubmit);

    const input = await screen.findByLabelText('Barcode');
    await user.type(input, 'DUPLICATE-1');
    await user.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(messageError).toHaveBeenCalledWith('Dieser Barcode ist bereits vergeben.');
    });
  });
});
