import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ExtensionManager } from '@/features/extensions/ExtensionManager';
import { HairSalonCampaignEditor } from '@/features/extensions/HairSalonCampaignEditor';
import { I18nProvider } from '@/i18n';

const mockSetExtension = vi.fn();
const mockCreateCampaign = vi.fn();
const extensionQuery = {
  data: [] as Array<{ name: string; enabled: boolean; hooks: string[] }>,
  isLoading: false,
  isError: false,
};
const campaignQuery = {
  data: [] as Array<Record<string, unknown>>,
  isLoading: false,
  isError: false,
};

vi.mock('@/api/admin/tenantExtensions', () => ({
  tenantExtensionQueryKeys: {
    all: ['admin', 'tenant-extensions'],
    list: () => ['admin', 'tenant-extensions', 'list'],
  },
  useTenantExtensions: () => extensionQuery,
  setTenantExtension: (...args: unknown[]) => mockSetExtension(...args),
  listTenantExtensions: vi.fn(),
}));

vi.mock('@/api/admin/hairSalonCampaigns', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/admin/hairSalonCampaigns')>();
  return {
    ...actual,
    useHairSalonCampaigns: () => campaignQuery,
    createHairSalonCampaign: (...args: unknown[]) => mockCreateCampaign(...args),
    listHairSalonCampaigns: vi.fn(),
  };
});

vi.mock('@/api/admin/products', () => ({
  useAdminProductsList: () => ({
    data: { items: [{ id: 'prod-1', name: 'Farbe' }] },
    isLoading: false,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { role: 'Manager', permissions: ['settings.view', 'settings.manage'] },
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    successKey: vi.fn(),
    apiError: vi.fn(),
  }),
}));

function renderWithProviders(node: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <I18nProvider>
      <App>
        <QueryClientProvider client={client}>{node}</QueryClientProvider>
      </App>
    </I18nProvider>
  );
}

describe('extension admin UI', () => {
  beforeEach(() => {
    mockSetExtension.mockReset();
    mockCreateCampaign.mockReset();
    extensionQuery.data = [];
    extensionQuery.isLoading = false;
    extensionQuery.isError = false;
    campaignQuery.data = [];
    campaignQuery.isLoading = false;
    campaignQuery.isError = false;
    mockSetExtension.mockResolvedValue({ name: 'hair_salon_campaign', enabled: true, hooks: [] });
    mockCreateCampaign.mockResolvedValue({ id: 'c1', name: 'Weekday' });
  });

  it('renders the extension list', async () => {
    extensionQuery.data = [
      {
        name: 'hair_salon_campaign',
        enabled: false,
        hooks: ['payment.before-sign'],
      },
    ];

    renderWithProviders(<ExtensionManager />);

    expect(await screen.findByText('hair_salon_campaign')).toBeInTheDocument();
    expect(
      screen.getByText('Rabattiert passende Warenkorbzeilen, bevor der Beleg signiert wird.')
    ).toBeInTheDocument();
    expect(screen.getByText('Deaktiviert')).toBeInTheDocument();
  });

  it('toggle calls PUT with name and enabled', async () => {
    extensionQuery.data = [
      { name: 'hair_salon_campaign', enabled: false, hooks: ['payment.before-sign'] },
    ];

    renderWithProviders(<ExtensionManager />);

    fireEvent.click(await screen.findByRole('switch', { name: 'hair_salon_campaign' }));

    await waitFor(() => {
      expect(mockSetExtension).toHaveBeenCalledWith({
        name: 'hair_salon_campaign',
        enabled: true,
      });
    });
  });

  it('hides the campaign editor when the extension is disabled', async () => {
    extensionQuery.data = [
      { name: 'hair_salon_campaign', enabled: false, hooks: ['payment.before-sign'] },
    ];

    renderWithProviders(<HairSalonCampaignEditor />);

    expect(
      await screen.findByText('Die Friseur-Kampagne ist für diesen Mandanten nicht aktiv.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Anlegen' })).not.toBeInTheDocument();
  });

  it('create campaign calls POST when the extension is enabled', async () => {
    extensionQuery.data = [
      { name: 'hair_salon_campaign', enabled: true, hooks: ['payment.before-sign'] },
    ];

    renderWithProviders(<HairSalonCampaignEditor />);

    fireEvent.click(await screen.findByRole('button', { name: 'Anlegen' }));
    fireEvent.change(await screen.findByLabelText('Name'), { target: { value: 'Weekday' } });
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(mockCreateCampaign).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Weekday',
          discountPercent: 10,
          appliesToProductIds: [],
        })
      );
    });
  });
});
