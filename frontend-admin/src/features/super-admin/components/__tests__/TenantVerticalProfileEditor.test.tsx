import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n';

import { TenantVerticalProfileEditor } from '../TenantVerticalProfileEditor';

const tenantId = '11111111-1111-1111-1111-111111111111';
const mockListProfiles = vi.fn();
const mockGetEffective = vi.fn();
const mockPutProfile = vi.fn();
const mockProfileImpact = vi.fn();
const mockNotifySuccess = vi.fn();
const mockNotifyError = vi.fn();

vi.mock('@/api/generated/admin/admin', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/generated/admin/admin')>();
  return {
    ...actual,
    getApiAdminVerticalProfiles: (...args: unknown[]) => mockListProfiles(...args),
    getApiAdminTenantsTenantIdVerticalProfile: (...args: unknown[]) =>
      mockGetEffective(...args),
    putApiAdminTenantsTenantIdVerticalProfile: (...args: unknown[]) =>
      mockPutProfile(...args),
  };
});

vi.mock('@/features/super-admin/tenantProfileImpact', () => ({
  getTenantProfileImpact: (...args: unknown[]) => mockProfileImpact(...args),
  impactWarningLabelKey: (code: string) => `tenants.verticalProfile.impact${code}`,
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: mockNotifySuccess,
    error: mockNotifyError,
  }),
}));

const profiles = [
  {
    id: 'gastronomy',
    name: 'verticalProfiles.gastronomy.name',
    posFeatures: { tables: false, kitchenDisplay: true },
    requiredFields: { customer: [], product: ['name'] },
    optionalFields: { customer: ['phone'], product: ['stock'] },
    posLayout: 'standard',
  },
  {
    id: 'gastronomy-tables',
    name: 'verticalProfiles.gastronomyTables.name',
    posFeatures: { tables: true, kitchenDisplay: true },
    requiredFields: { customer: [], product: ['name'] },
    optionalFields: { customer: ['phone'], product: ['stock'] },
    posLayout: 'tables',
  },
  {
    id: 'hair-salon',
    name: 'verticalProfiles.hairSalon.name',
    posFeatures: { appointment: true, serviceDuration: true },
    requiredFields: { customer: ['name'], product: ['name'] },
    optionalFields: { customer: ['email'], product: ['description'] },
    posLayout: 'appointment',
  },
  {
    id: 'taxi',
    name: 'verticalProfiles.taxi.name',
    posFeatures: { routeTracking: true },
    requiredFields: { customer: [], product: ['name'] },
    optionalFields: { customer: ['phone'], product: ['description'] },
    posLayout: 'taxi',
  },
];

const effective = {
  profileId: 'gastronomy',
  name: 'verticalProfiles.gastronomy.name',
  posFeatures: { tables: false, kitchenDisplay: true },
  requiredFields: { customer: [], product: ['name'] },
  optionalFields: { customer: ['phone'], product: ['stock'] },
  posLayout: 'standard',
  overrides: {},
};

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', ResizeObserverMock);
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation(() => ({
      matches: false,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

function renderEditor() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <App>
          <TenantVerticalProfileEditor tenantId={tenantId} />
        </App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('TenantVerticalProfileEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockListProfiles.mockResolvedValue(profiles);
    mockGetEffective.mockResolvedValue(effective);
    mockPutProfile.mockResolvedValue(effective);
    mockProfileImpact.mockResolvedValue({
      currentProfileId: 'gastronomy',
      targetProfileId: 'gastronomy',
      counts: {
        customersWithPetData: 0,
        paymentsWithPrescriptionReference: 0,
        paymentsWithRouteFrom: 0,
        soldImeis: 0,
        appointments: 0,
        rooms: 0,
        folios: 0,
        tickets: 0,
      },
      warnings: [],
    });
  });

  it('renders the profile selector with translated profiles', async () => {
    renderEditor();

    const selector = await screen.findByRole('combobox', { name: 'POS-Profil' });
    await waitFor(() => expect(selector).toHaveAttribute('aria-expanded', 'false'));
    fireEvent.mouseDown(selector);

    expect(await screen.findByText('Gastronomie mit Tischen')).toBeInTheDocument();
    expect(screen.getByText('Friseursalon')).toBeInTheDocument();
  });

  it('calls PUT when the profile is saved', async () => {
    renderEditor();

    const save = await screen.findByRole('button', { name: 'Profil speichern' });
    await waitFor(() => expect(save).toBeEnabled());
    fireEvent.click(save);

    await waitFor(() =>
      expect(mockPutProfile).toHaveBeenCalledWith(
        tenantId,
        expect.objectContaining({ profileId: 'gastronomy' })
      )
    );
    expect(mockNotifySuccess).toHaveBeenCalledWith('POS-Profil wurde gespeichert.');
  });

  it('invalidates the sidebar profile cache after PUT', async () => {
    const invalidateQueries = vi.spyOn(QueryClient.prototype, 'invalidateQueries');
    renderEditor();

    const save = await screen.findByRole('button', { name: 'Profil speichern' });
    await waitFor(() => expect(save).toBeEnabled());
    fireEvent.click(save);

    await waitFor(() => expect(mockPutProfile).toHaveBeenCalled());
    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: ['/api/admin/vertical-profile'],
    });
    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: ['admin', 'vertical-profile'],
    });
    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: ['admin', 'tenants', tenantId, 'vertical-profile'],
    });
    invalidateQueries.mockRestore();
  });

  it('shows the taxi tariff field when the taxi profile is selected', async () => {
    mockGetEffective.mockResolvedValue({
      ...effective,
      profileId: 'taxi',
      name: 'verticalProfiles.taxi.name',
      posFeatures: { routeTracking: true },
      posLayout: 'taxi',
      taxiTariffPerKm: 2.4,
    });

    renderEditor();

    expect(await screen.findByLabelText('Tarif pro Kilometer (EUR)')).toBeInTheDocument();
  });

  it('includes feature toggle overrides in the PUT payload', async () => {
    renderEditor();

    const tablesToggle = await screen.findByRole('switch', { name: 'Tische' });
    expect(tablesToggle).not.toBeChecked();
    fireEvent.click(tablesToggle);
    fireEvent.click(screen.getByRole('button', { name: 'Profil speichern' }));

    await waitFor(() =>
      expect(mockPutProfile).toHaveBeenCalledWith(
        tenantId,
        expect.objectContaining({
          overrides: expect.objectContaining({
            posFeatures: expect.objectContaining({ tables: true }),
          }),
        })
      )
    );
  });

  it('warns about hidden historical rows and saves only after confirmation', async () => {
    mockGetEffective.mockResolvedValue({
      ...effective,
      profileId: 'vet',
      name: 'verticalProfiles.vet.name',
      posFeatures: { patientRecord: true },
    });
    mockProfileImpact.mockResolvedValue({
      currentProfileId: 'vet',
      targetProfileId: 'gastronomy',
      counts: { customersWithPetData: 2, paymentsWithPrescriptionReference: 1 },
      warnings: [
        { code: 'customersWithPetData', count: 2 },
        { code: 'paymentsWithPrescriptionReference', count: 1 },
      ],
    });

    renderEditor();

    const selector = await screen.findByRole('combobox', { name: 'POS-Profil' });
    fireEvent.mouseDown(selector);
    fireEvent.click(await screen.findByText('Gastronomie'));

    expect(await screen.findByText('Historische Daten bleiben erhalten')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Profil speichern' }));

    await waitFor(() => expect(mockProfileImpact).toHaveBeenCalled());
    expect(mockPutProfile).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByRole('button', { name: 'Trotzdem speichern' }));
    await waitFor(() =>
      expect(mockPutProfile).toHaveBeenCalledWith(
        tenantId,
        expect.objectContaining({ profileId: 'gastronomy' })
      )
    );
  });
});
