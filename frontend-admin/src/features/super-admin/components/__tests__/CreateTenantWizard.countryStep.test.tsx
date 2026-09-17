/**
 * CreateTenantWizard country step: rendering, validation, AT path still reaches tenant form.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CountryProfileSummaryDto } from '@/api/generated/model';
import { CreateTenantWizard } from '@/features/super-admin/components/CreateTenantWizard';
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
    allowedVatRegimes: ['AT_RKSV_STANDARD', 'EU_REVERSE_CHARGE', 'EU_OSS', 'NON_EU'],
  },
  {
    code: 'DE',
    name: 'Germany',
    currency: 'EUR',
    defaultLocale: 'de-DE',
    fiscalSystem: 'KASSENSICHERHEIT_DE',
    eInvoicingStandards: ['ZUGFERD', 'XRECHNUNG'],
    allowedVatRegimes: ['DE_USTG_STANDARD', 'DE_KLEINUNTERNEHMER', 'EU_REVERSE_CHARGE', 'EU_OSS', 'NON_EU'],
  },
  {
    code: 'CH',
    name: 'Switzerland',
    currency: 'CHF',
    defaultLocale: 'de-CH',
    fiscalSystem: 'MWST_CH',
    eInvoicingStandards: ['QR_RECHNUNG'],
    allowedVatRegimes: ['CH_MWST_STANDARD', 'CH_KLEINUNTERNEHMER', 'NON_EU'],
  },
];

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

function renderWizard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const onClose = vi.fn();
  const onCreated = vi.fn();

  render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CreateTenantWizard open onClose={onClose} onCreated={onCreated} />
      </I18nProvider>
    </QueryClientProvider>
  );

  return { onClose, onCreated };
}

async function waitForCountryDefaults() {
  await waitFor(() => {
    expect(screen.getAllByRole('combobox').length).toBeGreaterThan(0);
  });
}

async function goToTenantForm(user: ReturnType<typeof userEvent.setup>) {
  await waitForCountryDefaults();
  await user.click(screen.getByRole('button', { name: 'Weiter' }));
  await waitFor(() => {
    expect(screen.getByTestId('create-tenant-form-panel')).toHaveStyle({ display: 'block' });
    expect(screen.getByRole('button', { name: 'Zurück' })).toBeInTheDocument();
  });
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
      removeEventListener: vi.fn(),
      addEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('CreateTenantWizard country step', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCreateAdminTenant.mockResolvedValue({
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Cafe Muster',
      slug: 'cafe-muster',
      email: 'info@cafe-muster.at',
      status: 'active',
      isActive: true,
      createdAt: new Date().toISOString(),
    });
  });

  it('renders country and VAT regime fields with a two-step indicator', async () => {
    renderWizard();

    expect(await screen.findByRole('button', { name: 'Weiter' })).toBeInTheDocument();
    expect(screen.getAllByText('Land').length).toBeGreaterThan(0);
    expect(screen.getByText('Mandant')).toBeInTheDocument();
    expect(screen.getByLabelText('Land')).toBeInTheDocument();
    expect(screen.getByLabelText('Umsatzsteuer-Regime')).toBeInTheDocument();
    expect(screen.getByTestId('create-tenant-country-panel')).toHaveStyle({ display: 'block' });
    expect(screen.getByTestId('create-tenant-form-panel')).toHaveStyle({ display: 'none' });
  });

  it('shows the non-AT banner for DE and hides it for AT', async () => {
    const user = userEvent.setup();
    renderWizard();

    expect(
      screen.queryByText(/Nicht-AT-Mandanten aktivieren RKSV\/TSE nicht/)
    ).not.toBeInTheDocument();

    await waitForCountryDefaults();
    const countrySelect = screen.getByLabelText('Land');
    await user.click(countrySelect);
    await user.click(await screen.findByText('DE — Germany'));

    expect(
      await screen.findByText(/Nicht-AT-Mandanten aktivieren RKSV\/TSE nicht/)
    ).toBeInTheDocument();

    await user.click(screen.getByLabelText('Land'));
    await user.click(await screen.findByText('AT — Austria'));

    await waitFor(() => {
      expect(
        screen.queryByText(/Nicht-AT-Mandanten aktivieren RKSV\/TSE nicht/)
      ).not.toBeInTheDocument();
    });
  });

  it('blocks advancing when country is cleared', async () => {
    const user = userEvent.setup();
    renderWizard();

    await waitForCountryDefaults();
    const countrySelect = screen.getByLabelText('Land');
    await user.hover(countrySelect);
    const clear = document.querySelector('.ant-select-clear');
    if (clear) {
      await user.click(clear);
    }

    await user.click(screen.getByRole('button', { name: 'Weiter' }));

    expect(await screen.findByText('Bitte ein Land wählen.')).toBeInTheDocument();
    expect(screen.getByTestId('create-tenant-form-panel')).toHaveStyle({ display: 'none' });
  });

  it('AT path reaches the tenant form unchanged', async () => {
    const user = userEvent.setup();
    renderWizard();

    await goToTenantForm(user);

    expect(screen.getByTestId('create-tenant-form-panel')).toHaveStyle({ display: 'block' });
    expect(screen.getByLabelText('Firmenname')).toBeInTheDocument();
    expect(screen.getByLabelText('E-Mail (Kontakt)')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('cafe-beispiel')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Zurück' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kunden anlegen' })).toBeInTheDocument();
    expect(screen.getByTestId('create-tenant-country-panel')).toHaveStyle({ display: 'none' });
  });

  it(
    'submits AT countryCode and vatRegime from the tenant form',
    async () => {
      const user = userEvent.setup();
      renderWizard();

      await goToTenantForm(user);
      const nameInput = screen.getByLabelText('Firmenname');
      const modal = nameInput.closest('.ant-modal') as HTMLElement;

      await user.type(within(modal).getByLabelText('Firmenname'), 'Cafe Muster');
      fireEvent.blur(within(modal).getByLabelText('Firmenname'));
      await user.type(within(modal).getByLabelText('E-Mail (Kontakt)'), 'info@cafe-muster.at');
      const slugInput = within(modal).getByPlaceholderText('cafe-beispiel');
      await user.clear(slugInput);
      await user.type(slugInput, 'cafe-muster');
      fireEvent.blur(slugInput);

      await waitFor(
        () => {
          expect(within(modal).getByRole('button', { name: 'Kunden anlegen' })).not.toBeDisabled();
        },
        { timeout: 8_000 }
      );

      await user.click(within(modal).getByRole('button', { name: 'Kunden anlegen' }));

      await waitFor(() => {
        expect(mockCreateAdminTenant).toHaveBeenCalled();
      });

      const body = mockCreateAdminTenant.mock.calls[0]?.[0] as {
        countryCode: string;
        vatRegime: string;
        name: string;
      };
      expect(body.countryCode).toBe('AT');
      expect(body.vatRegime).toBe('AT_RKSV_STANDARD');
      expect(body.name).toBe('Cafe Muster');
    },
    20_000
  );
});
