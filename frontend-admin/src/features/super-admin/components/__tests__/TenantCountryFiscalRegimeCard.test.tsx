/**
 * TenantCountryFiscalRegimeCard — view vs Super Admin edit + confirmation modal.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { TenantCountryFiscalRegimeCard } from '@/features/super-admin/components/TenantCountryFiscalRegimeCard';
import { I18nProvider } from '@/i18n';

const mockUpdateCountry = vi.fn();
const mockUseAuth = vi.fn();
const lastConfirm: { current: { title?: React.ReactNode; content?: React.ReactNode; onOk?: () => unknown } | null } =
  { current: null };

const COUNTRIES: CountryProfileSummaryDto[] = [
  {
    code: 'AT',
    name: 'Austria',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'RKSV_AT',
    eInvoicingStandards: [],
    allowedVatRegimes: ['AT_RKSV_STANDARD', 'EU_REVERSE_CHARGE'],
  },
  {
    code: 'DE',
    name: 'Germany',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'KASSENSICHERHEIT_DE',
    eInvoicingStandards: [],
    allowedVatRegimes: ['DE_USTG_STANDARD'],
  },
];

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/tenancy/hooks/useCountries', () => ({
  useCountries: () => ({ data: COUNTRIES, isLoading: false }),
}));

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
  return {
    ...actual,
    updateAdminTenantCountry: (...args: unknown[]) => mockUpdateCountry(...args),
  };
});

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
    modal: {
      confirm: (opts: { title?: React.ReactNode; content?: React.ReactNode; onOk?: () => unknown }) => {
        lastConfirm.current = opts;
      },
    },
    notification: {},
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    successKey: vi.fn(),
    errorKey: vi.fn(),
    apiError: vi.fn(),
  }),
}));

const tenant: AdminTenantDetail = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Cafe Demo',
  slug: 'cafe-demo',
  status: 'active',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  country: 'AT',
  vatRegime: 'AT_RKSV_STANDARD',
  vatId: 'ATU12345678',
  billingCountry: 'DE',
  taxExempt: false,
};

function renderCard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <TenantCountryFiscalRegimeCard tenant={tenant} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', ResizeObserverMock);

  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('TenantCountryFiscalRegimeCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    lastConfirm.current = null;
    mockUpdateCountry.mockResolvedValue({ ...tenant, vatRegime: 'EU_REVERSE_CHARGE' });
  });

  it('shows country fields and hides edit for Mandanten-Admin', () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Manager' } });
    renderCard();

    expect(screen.getByTestId('tenant-country-card')).toBeInTheDocument();
    expect(screen.getByText('AT')).toBeInTheDocument();
    expect(screen.getByText('AT_RKSV_STANDARD')).toBeInTheDocument();
    expect(screen.getByText('ATU12345678')).toBeInTheDocument();
    expect(screen.getByText('DE')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-country-view-only')).toBeInTheDocument();
    expect(screen.queryByTestId('tenant-country-edit')).not.toBeInTheDocument();
  });

  it('lets Super Admin open the confirmation modal before saving', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'SuperAdmin' } });
    renderCard();

    fireEvent.click(screen.getByTestId('tenant-country-edit'));
    fireEvent.click(screen.getByTestId('tenant-country-save'));

    await waitFor(() => {
      expect(lastConfirm.current).not.toBeNull();
    });
    expect(String(lastConfirm.current?.title)).toMatch(/Fiskalsystem/);
    expect(String(lastConfirm.current?.content)).toMatch(/Historische Rechnungen/);
    expect(mockUpdateCountry).not.toHaveBeenCalled();

    await lastConfirm.current?.onOk?.();
    await waitFor(() => {
      expect(mockUpdateCountry).toHaveBeenCalledWith(tenant.id, {
        country: 'AT',
        vatRegime: 'AT_RKSV_STANDARD',
      });
    });
  });
});
