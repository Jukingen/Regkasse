import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ProductForm from '@/features/products/components/ProductForm';
import { I18nProvider } from '@/i18n';

const mockUseAmbientVerticalProfile = vi.fn();
const mockUseAdminStaff = vi.fn();
const mockUseAdminProductImeis = vi.fn();

vi.mock('@/api/admin/vertical-profile', () => ({
  useAmbientVerticalProfile: (...args: unknown[]) => mockUseAmbientVerticalProfile(...args),
}));

vi.mock('@/api/admin/staff', () => ({
  useAdminStaff: (...args: unknown[]) => mockUseAdminStaff(...args),
}));

vi.mock('@/api/admin/product-imeis', () => ({
  useAdminProductImeis: (...args: unknown[]) => mockUseAdminProductImeis(...args),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { error: vi.fn(), success: vi.fn() },
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

function renderForm(props?: { isEditMode?: boolean; initialValues?: { id: string } }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <ProductForm
          visible
          isEditMode={props?.isEditMode}
          initialValues={props?.initialValues as never}
          onCancel={() => undefined}
          onSubmit={async () => undefined}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('ProductForm duration and staff fields', () => {
  beforeEach(() => {
    mockUseAdminStaff.mockReturnValue({
      data: [{ id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' }],
      isLoading: false,
    });
    mockUseAdminProductImeis.mockReturnValue({
      data: [],
      isLoading: false,
    });
  });

  it('hides duration and staff when the profile has neither serviceDuration nor appointment', () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { posFeatures: { kitchenDisplay: true, patientRecord: false } },
    });

    renderForm();

    expect(screen.queryByText('Dauer (Minuten)')).toBeNull();
    expect(screen.queryByText('Mitarbeiter wählen')).toBeNull();
    expect(screen.queryByText('IMEI-pflichtig')).toBeNull();
  });

  it('shows duration and staff when serviceDuration is enabled', async () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { posFeatures: { serviceDuration: true, appointment: true } },
    });

    renderForm();

    expect(await screen.findByText('Dauer (Minuten)')).toBeTruthy();
    expect(await screen.findByText('Mitarbeiter wählen')).toBeTruthy();
  });

  it('shows the IMEI-tracked toggle when imeiTracking is enabled', async () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { posFeatures: { imeiTracking: true } },
    });

    renderForm();

    expect(await screen.findByText('IMEI-pflichtig')).toBeTruthy();
  });

  it('shows the ticket toggle when the tenant profile is ticket-sales', async () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { profileId: 'ticket-sales', posFeatures: { ticketScan: true } },
    });

    renderForm();

    expect(await screen.findByText('Ticket')).toBeTruthy();
  });

  it('hides the ticket toggle when the profile is not ticket-sales', () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { profileId: 'gastronomy', posFeatures: { kitchenDisplay: true } },
    });

    renderForm();

    expect(screen.queryByText('Ticket')).toBeNull();
  });

  it('lists IMEIs with status on the product detail form', async () => {
    mockUseAmbientVerticalProfile.mockReturnValue({
      data: { posFeatures: { imeiTracking: true } },
    });
    mockUseAdminProductImeis.mockReturnValue({
      data: [{ id: 'imei-1', imei: '490154203237518', status: 'Sold' }],
      isLoading: false,
    });

    renderForm({ isEditMode: true, initialValues: { id: 'prod-1' } });

    expect(await screen.findByText('IMEI-Bestand')).toBeTruthy();
    expect(await screen.findByText('490154203237518')).toBeTruthy();
    expect(await screen.findByText('Verkauft')).toBeTruthy();
  });
});
