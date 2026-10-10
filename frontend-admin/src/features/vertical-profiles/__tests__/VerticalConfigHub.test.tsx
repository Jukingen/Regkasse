import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n';

import { VerticalConfigHub } from '../VerticalConfigHub';

const mockList = vi.fn();
const mockUpdate = vi.fn();
const mockUpdateFeatures = vi.fn();
const mockDelete = vi.fn();
const mockTenants = vi.fn();
const mockGroups = vi.fn();
const mockNotifySuccess = vi.fn();
const mockNotifyError = vi.fn();

vi.mock('../api', () => ({
  listVerticalProfiles: (...args: unknown[]) => mockList(...args),
  updateVerticalProfile: (...args: unknown[]) => mockUpdate(...args),
  updateVerticalProfileFeatures: (...args: unknown[]) => mockUpdateFeatures(...args),
  deleteVerticalProfile: (...args: unknown[]) => mockDelete(...args),
  listVerticalProfileTenants: (...args: unknown[]) => mockTenants(...args),
  listTenantsByVerticalProfile: (...args: unknown[]) => mockGroups(...args),
  createVerticalProfile: vi.fn(),
  cloneVerticalProfile: vi.fn(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: mockNotifySuccess,
    error: mockNotifyError,
    warning: vi.fn(),
  }),
}));

const profiles = [
  {
    id: 'gastronomy',
    name: 'verticalProfiles.gastronomy.name',
    posFeatures: { kitchenDisplay: true, tables: false },
    requiredFields: { customer: [], product: ['name'], order: [] },
    optionalFields: { customer: ['phone'], product: [], order: [] },
    posLayout: 'standard',
    featureCount: 1,
    tenantCount: 1,
    source: 'seed',
  },
  {
    id: 'bakery',
    name: 'Bakery',
    posFeatures: { kitchenDisplay: false, tables: false },
    requiredFields: { customer: [], product: [], order: [] },
    optionalFields: { customer: [], product: [], order: [] },
    posLayout: 'standard',
    featureCount: 0,
    tenantCount: 1,
    source: 'custom',
  },
];

const bakeryTenants = [
  { id: '22222222-2222-2222-2222-222222222222', name: 'Bakery Tenant', slug: 'bakery', overridesCount: 1 },
];

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

function renderHub() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <App>
          <VerticalConfigHub />
        </App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('VerticalConfigHub', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockList.mockResolvedValue(profiles);
    mockTenants.mockResolvedValue(bakeryTenants);
    mockGroups.mockResolvedValue([]);
    mockUpdate.mockResolvedValue({
      profile: profiles[0],
      removedFeatures: [],
      affectedTenants: [],
    });
    mockUpdateFeatures.mockResolvedValue({
      profile: profiles[0],
      removedFeatures: ['kitchenDisplay'],
      affectedTenants: bakeryTenants,
    });
  });

  it('renders the profile list', async () => {
    renderHub();
    expect(await screen.findByTestId('profile-id-gastronomy')).toBeInTheDocument();
    expect(screen.getByTestId('profile-id-bakery')).toBeInTheDocument();
  });

  it('saves the editor', async () => {
    renderHub();
    fireEvent.click(await screen.findByRole('button', { name: 'Bearbeiten gastronomy' }));
    const name = await screen.findByRole('textbox', { name: 'Name' });
    fireEvent.change(name, { target: { value: 'Edited gastronomy' } });
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() =>
      expect(mockUpdate).toHaveBeenCalledWith(
        'gastronomy',
        expect.objectContaining({ name: 'Edited gastronomy' })
      )
    );
  });

  it('invalidates the sidebar profile cache after a catalog save', async () => {
    const invalidateQueries = vi.spyOn(QueryClient.prototype, 'invalidateQueries');
    renderHub();
    fireEvent.click(await screen.findByRole('button', { name: 'Bearbeiten gastronomy' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Speichern' }));

    await waitFor(() => expect(mockUpdate).toHaveBeenCalled());
    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: ['/api/admin/vertical-profile'],
    });
    invalidateQueries.mockRestore();
  });

  it('toggles a feature in the matrix', async () => {
    renderHub();
    fireEvent.click(await screen.findByRole('tab', { name: 'Funktionsmatrix' }));
    const toggle = await screen.findByRole('switch', { name: 'gastronomy kitchenDisplay' });
    fireEvent.click(toggle);

    await waitFor(() =>
      expect(mockUpdateFeatures).toHaveBeenCalledWith(
        'gastronomy',
        expect.objectContaining({ kitchenDisplay: false })
      )
    );
  });

  it('warns when deleting a profile that is in use', async () => {
    renderHub();
    fireEvent.click(await screen.findByRole('button', { name: 'Löschen bakery' }));

    expect(await screen.findByText('Bakery Tenant')).toBeInTheDocument();
    expect(screen.getByText('Profil wird verwendet')).toBeInTheDocument();
    expect(mockDelete).not.toHaveBeenCalled();
  });
});
