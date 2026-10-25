import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { FolioDetail } from '@/features/lodging/FolioDetail';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const refetchFolio = vi.fn();
const refetchRooms = vi.fn();
const moveAdminFolio = vi.fn();

vi.mock('@/api/admin/vertical-profile', () => ({
  useAmbientVerticalProfile: () => ({
    data: { profileId: 'beherbergung', posFeatures: { roomTracking: true } },
    isFetched: true,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { permissions: [PERMISSIONS.PRODUCT_MANAGE] } }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({ modal: { confirm: vi.fn() } }),
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
      status: 'Open',
    },
    isError: false,
    isLoading: false,
    refetch: refetchFolio,
  }),
  useAdminFolioItems: () => ({ data: [], isLoading: false }),
  useAdminRooms: () => ({
    data: [
      { id: 'room-1', number: '101', type: 'DZ', capacity: 2, status: 'Occupied', isActive: true, occupied: true },
      { id: 'room-2', number: '102', type: 'EZ', capacity: 1, status: 'Available', isActive: true, occupied: false },
    ],
    isLoading: false,
    refetch: refetchRooms,
  }),
  useChargeAdminFolio: () => ({ mutate: vi.fn(), isPending: false }),
  useCheckoutAdminFolio: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useMoveAdminFolio: () => ({
    mutate: (input: { id: string; newRoomId: string }, options?: { onSuccess?: () => void }) => {
      moveAdminFolio(input);
      options?.onSuccess?.();
    },
    isPending: false,
  }),
}));

describe('folio room move', () => {
  it('changes the room and refreshes the folio', async () => {
    render(
      <I18nProvider>
        <App>
          <FolioDetail folioId="folio-1" />
        </App>
      </I18nProvider>
    );

    expect(screen.getByRole('button', { name: 'Zimmer wechseln' })).toBeDisabled();
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Neue Zimmernummer' }));
    fireEvent.click(await screen.findByText('102'));
    fireEvent.click(screen.getByRole('button', { name: 'Zimmer wechseln' }));

    await waitFor(() => {
      expect(moveAdminFolio).toHaveBeenCalledWith({ id: 'folio-1', newRoomId: 'room-2' });
    });
    expect(refetchFolio).toHaveBeenCalled();
    expect(refetchRooms).toHaveBeenCalled();
  });
});
