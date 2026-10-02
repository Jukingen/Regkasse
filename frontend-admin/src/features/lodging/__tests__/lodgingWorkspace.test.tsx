import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { LodgingWorkspace } from '@/features/lodging/LodgingWorkspace';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

vi.mock('@/api/admin/vertical-profile', () => ({
  useAmbientVerticalProfile: () => ({
    data: { profileId: 'beherbergung', posFeatures: { roomTracking: true } },
    isFetched: true,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { permissions: [PERMISSIONS.PRODUCT_VIEW, PERMISSIONS.PRODUCT_MANAGE] } }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/api/admin/lodging', () => ({
  useAdminRooms: () => ({
    data: [
      {
        id: 'room-1',
        number: '101',
        type: 'DZ',
        capacity: 2,
        isActive: true,
        occupied: true,
      },
    ],
    isLoading: false,
    isError: false,
  }),
  useAdminFolios: () => ({
    data: [
      {
        id: 'folio-1',
        customerId: 'cust-1',
        customerName: 'Anna Gast',
        roomId: 'room-1',
        roomNumber: '101',
        checkIn: '2026-10-01T12:00:00Z',
        checkOut: null,
        balance: 80,
        isOpen: true,
      },
    ],
    isLoading: false,
    isError: false,
  }),
  useCreateAdminRoom: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

describe('lodging workspace', () => {
  it('requires product.view on /admin/rooms', () => {
    expect(getRequiredPermissionForPath('/admin/rooms')).toEqual(PERMISSIONS.PRODUCT_VIEW);
  });

  it('renders rooms and guest folios', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <I18nProvider>
        <QueryClientProvider client={client}>
          <App>
            <LodgingWorkspace />
          </App>
        </QueryClientProvider>
      </I18nProvider>
    );

    expect((await screen.findAllByText('Zimmer')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('101').length).toBeGreaterThan(0);
    expect(screen.getByText('Anna Gast')).toBeTruthy();
    expect(screen.getByText('Belegt')).toBeTruthy();
    expect(screen.getByText('Offen')).toBeTruthy();
    expect(screen.getByText('80.00')).toBeTruthy();
    expect(screen.getAllByText('Auslastung').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Zimmer anlegen' })).toBeTruthy();
  });
});
