/**
 * Demo vs regular tenant create: seedDemo payload and success copy.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import { CreateTenantWizard } from '@/features/super-admin/components/CreateTenantWizard';
import { buildDemoTenantDefaults } from '@/features/super-admin/lib/demoTenantDefaults';
import { I18nProvider } from '@/i18n';

const { mockCreateAdminTenant, mockCheckSlug } = vi.hoisted(() => ({
  mockCreateAdminTenant: vi.fn(),
  mockCheckSlug: vi.fn(async (slug: string) => ({
    normalizedSlug: slug,
    isValid: true,
    available: true,
  })),
}));

const COUNTRIES: CountryProfileSummaryDto[] = [
  {
    code: 'AT',
    name: 'Austria',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'RKSV_AT',
    eInvoicingStandards: [],
    allowedVatRegimes: ['AT_RKSV_STANDARD'],
  },
];

vi.mock('@/hooks/useDebounce', () => ({
  useDebounce: <T,>(value: T) => value,
}));

vi.mock('@/features/tenancy/hooks/useCountries', () => ({
  useCountries: () => ({ data: COUNTRIES, isLoading: false }),
}));

vi.mock('@/features/super-admin/api/adminTenants', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/super-admin/api/adminTenants')>();
  return {
    ...actual,
    createAdminTenant: (...args: unknown[]) => mockCreateAdminTenant(...args),
    checkAdminTenantSlugAvailability: (...args: unknown[]) => mockCheckSlug(...args),
    getAdminTenantSlugSuggestions: vi.fn(async () => []),
  };
});

function renderWizard(demoSeed = false) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CreateTenantWizard open onClose={vi.fn()} demoSeed={demoSeed} />
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

describe('buildDemoTenantDefaults', () => {
  it('formats the operator date and a sortable slug', () => {
    expect(buildDemoTenantDefaults(new Date(2026, 9, 26, 11, 32))).toEqual({
      name: 'Demo 2026-10-26',
      slug: 'demo-202610261132',
    });
  });
});

describe('CreateTenantWizard seedDemo', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCheckSlug.mockImplementation(async (slug: string) => ({
      normalizedSlug: slug,
      isValid: true,
      available: true,
    }));
  });

  it('prefills Austria, a 14-day trial, and sends seedDemo true', async () => {
    const user = userEvent.setup();
    mockCreateAdminTenant.mockResolvedValue({
      id: '22222222-2222-2222-2222-222222222222',
      name: 'Demo 2026-10-26',
      slug: 'demo-202610261132',
      email: 'demo@example.com',
      status: 'active',
      isActive: true,
      createdAt: '2026-10-26T10:00:00.000Z',
      provisioning: {
        cashRegisterId: '33333333-3333-3333-3333-333333333333',
        cashRegisterNumber: 'KASSE-001',
        adminUserId: 'admin-1',
        adminEmail: 'admin@demo.example',
        generatedPassword: 'Once-Secret-1',
        categoryId: 'cat-1',
        productIds: [],
      },
    });
    renderWizard(true);

    await waitFor(() => {
      expect(screen.getByDisplayValue(/^Demo \d{4}-\d{2}-\d{2}$/)).toBeInTheDocument();
    });
    expect(screen.getByText('14 Tage')).toBeInTheDocument();
    expect(screen.getByText('AT — Austria')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Weiter' }));
    const email = screen.getByPlaceholderText('geschaeftsfuehrer@cafe-beispiel.at');
    await user.type(email, 'demo@example.com');

    const submit = screen.getByRole('button', { name: 'Kunden anlegen' });
    await waitFor(() => expect(submit).toBeEnabled());
    await user.click(submit);

    await waitFor(() => expect(mockCreateAdminTenant).toHaveBeenCalled());
    expect(mockCreateAdminTenant.mock.calls[0][0]).toMatchObject({
      countryCode: 'AT',
      trialDurationDays: 14,
      grantTrialLicense: true,
      seedDemo: true,
      importDemoMenu: false,
    });
    expect(String(mockCreateAdminTenant.mock.calls[0][0].name)).toMatch(/^Demo \d{4}-\d{2}-\d{2}$/);

    expect(
      await screen.findByText('46 Produkte, 22 Kunden, KASSE-001 ist geöffnet. Demo bereit.')
    ).toBeInTheDocument();
    expect(screen.getByText('admin@demo.example')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Once-Secret-1')).toBeInTheDocument();
  }, 30_000);

  it('sends seedDemo false for a regular create', async () => {
    mockCreateAdminTenant.mockResolvedValue({
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Cafe Muster',
      slug: 'cafe-muster',
      email: 'info@cafe-muster.at',
      status: 'active',
      isActive: true,
      createdAt: '2026-10-26T10:00:00.000Z',
    });
    renderWizard(false);

    await waitFor(() => {
      expect(screen.getByText('AT — Austria')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Weiter' }));
    const submit = await screen.findByRole('button', { name: 'Kunden anlegen' });
    fireEvent.change(screen.getByPlaceholderText('Café Beispiel'), {
      target: { value: 'Cafe Muster' },
    });
    fireEvent.change(screen.getByPlaceholderText('cafe-beispiel'), {
      target: { value: 'cafe-muster' },
    });
    fireEvent.change(screen.getByPlaceholderText('geschaeftsfuehrer@cafe-beispiel.at'), {
      target: { value: 'info@cafe-muster.at' },
    });

    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);

    await waitFor(() => expect(mockCreateAdminTenant).toHaveBeenCalled());
    expect(mockCreateAdminTenant.mock.calls[0][0].seedDemo).toBe(false);
  });
});
