import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { KitchenWorkspace } from '@/features/kitchen/KitchenWorkspace';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

const mockGetSettings = vi.fn();
const mockUpdateSettings = vi.fn();
const mockListOrders = vi.fn();
const mockGetAnalytics = vi.fn();
const mockNotifySuccess = vi.fn();
const mockNotifyError = vi.fn();

vi.mock('@/api/admin/kitchen', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/admin/kitchen')>();
  return {
    ...actual,
    getKitchenSettings: (...args: unknown[]) => mockGetSettings(...args),
    updateKitchenSettings: (...args: unknown[]) => mockUpdateSettings(...args),
    listKitchenOrders: (...args: unknown[]) => mockListOrders(...args),
    getKitchenAnalytics: (...args: unknown[]) => mockGetAnalytics(...args),
    useKitchenSettings: () => ({
      data: { autoClearMinutes: 30, soundEnabled: true },
      isLoading: false,
    }),
    useKitchenOrders: () => ({
      data: [
        {
          id: 'ord-1',
          tableNumber: '12',
          status: 'Pending',
          notes: 'Ohne Zwiebel',
          createdAtUtc: '2026-10-01T00:00:00Z',
          items: [{ id: 'item-1', productName: 'Schnitzel', quantity: 1, status: 'Pending' }],
        },
      ],
      isLoading: false,
    }),
    useKitchenAnalytics: () => ({
      data: {
        averagePrepMinutes: 8.5,
        ordersPerHour: 1.2,
        createdLast24Hours: 29,
        createdLastHour: 3,
      },
      isLoading: false,
    }),
  };
});

vi.mock('@/api/admin/vertical-profile', () => ({
  useAmbientVerticalProfile: () => ({
    data: { posFeatures: { kitchenDisplay: true } },
    isFetched: true,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { role: 'SuperAdmin', permissions: ['settings.view', 'settings.manage'] },
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: mockNotifySuccess,
    error: mockNotifyError,
  }),
}));

function renderWorkspace() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <KitchenWorkspace requireKitchenDisplay={false} />
        </App>
      </QueryClientProvider>
    </I18nProvider>
  );
}

describe('kitchen workspace', () => {
  beforeEach(() => {
    mockGetSettings.mockReset();
    mockUpdateSettings.mockReset();
    mockListOrders.mockReset();
    mockGetAnalytics.mockReset();
    mockNotifySuccess.mockReset();
    mockNotifyError.mockReset();
    mockUpdateSettings.mockResolvedValue({ autoClearMinutes: 45, soundEnabled: false });
  });

  it('requires settings.view on /admin/kitchen', () => {
    expect(getRequiredPermissionForPath('/admin/kitchen')).toEqual(PERMISSIONS.SETTINGS_VIEW);
  });

  it('saves kitchen settings', async () => {
    renderWorkspace();
    expect(await screen.findByLabelText('Automatisch ausblenden (Minuten)')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));
    await waitFor(() => {
      expect(mockUpdateSettings).toHaveBeenCalledWith(
        expect.objectContaining({ autoClearMinutes: 30, soundEnabled: true }),
        {}
      );
    });
  });

  it('renders the live kitchen order list', async () => {
    renderWorkspace();
    expect(await screen.findByText('Offene Küchenbestellungen')).toBeTruthy();
    expect(screen.getByText('12')).toBeTruthy();
    expect(screen.getByText('Ohne Zwiebel')).toBeTruthy();
    expect(screen.getByText('1 × Schnitzel')).toBeTruthy();
  });
});
