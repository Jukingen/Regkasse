import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { FolioDetail } from '@/features/lodging/FolioDetail';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

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
  useAdminFolio: () => ({
    data: {
      id: 'folio-1',
      customerId: 'cust-1',
      customerName: 'Anna Gast',
      roomId: 'room-1',
      roomNumber: '101',
      checkIn: '2026-10-01T12:00:00Z',
      checkOut: null,
      balance: 42.5,
      isOpen: true,
    },
    isLoading: false,
    isError: false,
  }),
  useAdminFolioItems: () => ({
    data: [
      {
        id: 'item-1',
        folioId: 'folio-1',
        description: 'Minibar',
        amount: 12.5,
        createdAtUtc: '2026-10-02T08:00:00Z',
      },
      {
        id: 'item-2',
        folioId: 'folio-1',
        description: 'Frühstück',
        amount: 30,
        createdAtUtc: '2026-10-02T09:00:00Z',
      },
    ],
    isLoading: false,
    isError: false,
  }),
  useChargeAdminFolio: () => ({ mutate: vi.fn(), isPending: false }),
  useCheckoutAdminFolio: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

describe('folio detail', () => {
  it('renders charges from the folio', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <I18nProvider>
        <QueryClientProvider client={client}>
          <App>
            <FolioDetail folioId="folio-1" />
          </App>
        </QueryClientProvider>
      </I18nProvider>
    );

    expect(await screen.findByText('Minibar')).toBeTruthy();
    expect(screen.getByText('Frühstück')).toBeTruthy();
    expect(screen.getByText('12.50')).toBeTruthy();
    expect(screen.getByText('30.00')).toBeTruthy();
    expect(screen.getAllByText('42.50').length).toBeGreaterThan(0);
  });
});
