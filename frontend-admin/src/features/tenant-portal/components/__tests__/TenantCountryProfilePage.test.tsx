import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { CompanySettings } from '@/api/generated/model';
import { TenantCountryProfilePage } from '@/features/tenant-portal/components/TenantCountryProfilePage';
import { I18nProvider } from '@/i18n';

const mockUseAuth = vi.fn();
const mockUseCompanySettings = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => mockUseCompanySettings(),
}));

vi.mock('@/features/tenancy/hooks/useCountries', () => ({
  useCountries: () => ({ data: [], isLoading: false }),
}));

const settings = {
  tenantId: '22222222-2222-4222-8222-222222222222',
  companyName: 'Dev Tenant',
  companyTaxNumber: 'ATU12345678',
  country: 'AT',
  vatRegime: 'AT_RKSV_STANDARD',
  vatId: 'ATU12345678',
  billingCountry: null,
  taxExempt: false,
  createdAt: '2026-01-01T00:00:00Z',
} as CompanySettings;

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>{children}</I18nProvider>
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

describe('TenantCountryProfilePage', () => {
  it('shows the country card read-only for Mandanten-Admin', () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Manager' } });
    mockUseCompanySettings.mockReturnValue({
      data: settings,
      isLoading: false,
      isError: false,
    });

    render(<TenantCountryProfilePage />, { wrapper: Wrapper });

    expect(screen.getByTestId('tenant-country-card')).toBeInTheDocument();
    expect(screen.getByText('AT')).toBeInTheDocument();
    expect(screen.getByText('AT_RKSV_STANDARD')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-country-view-only')).toBeInTheDocument();
    expect(screen.queryByTestId('tenant-country-edit')).not.toBeInTheDocument();
    expect(screen.queryByTestId('tenant-country-save')).not.toBeInTheDocument();
  });
});
