import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';

import KitchenDisplayScreen from '../app/(screens)/kitchen-display';
import { changeLanguage, i18nReady } from '../i18n';
import type { KitchenOrder } from '../services/api/kitchenOrderService';

const mockList = jest.fn<(...args: unknown[]) => Promise<KitchenOrder[]>>();
const mockUpdateOrder = jest.fn<(...args: unknown[]) => Promise<KitchenOrder>>();
const mockUpdateItem = jest.fn<(...args: unknown[]) => Promise<KitchenOrder>>();

let hubOnCreated: ((order: KitchenOrder) => void) | undefined;

const verticalProfile = {
  current: {
    posFeatures: { kitchenDisplay: true } as Record<string, boolean>,
  },
};

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => verticalProfile.current,
}));

jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: {
      id: 'user-1',
      role: 'Kitchen',
      permissions: ['kitchen.view', 'kitchen.update'],
    },
  }),
}));

jest.mock('../hooks/useKitchenHub', () => ({
  useKitchenHub: (options: { onCreated?: (order: KitchenOrder) => void }) => {
    hubOnCreated = options.onCreated;
    return 'connected';
  },
  KitchenHubBridge: () => null,
}));

jest.mock('../services/api/kitchenOrderService', () => ({
  listKitchenOrders: (...args: unknown[]) => mockList(...args),
  updateKitchenOrderStatus: (...args: unknown[]) => mockUpdateOrder(...args),
  updateKitchenOrderItemStatus: (...args: unknown[]) => mockUpdateItem(...args),
}));

function pendingOrder(overrides: Partial<KitchenOrder> = {}): KitchenOrder {
  return {
    id: 'ord-1',
    tableNumber: '12',
    cashRegisterId: 'reg-1',
    status: 'Pending',
    priority: 0,
    notes: 'Ohne Zwiebel',
    createdAtUtc: new Date().toISOString(),
    items: [
      {
        id: 'item-1',
        productName: 'Schnitzel',
        quantity: 1,
        status: 'Pending',
        notes: 'Paniert',
      },
    ],
    ...overrides,
  };
}

describe('kitchen display screen', () => {
  beforeEach(async () => {
    cleanup();
    jest.spyOn(global, 'setInterval').mockReturnValue(0 as unknown as ReturnType<typeof setInterval>);
    jest.spyOn(global, 'clearInterval').mockImplementation(() => undefined);
    await i18nReady;
    await changeLanguage('de');
    mockList.mockReset();
    mockUpdateOrder.mockReset();
    mockUpdateItem.mockReset();
    hubOnCreated = undefined;
    verticalProfile.current.posFeatures = { kitchenDisplay: true };
    mockList.mockResolvedValue([pendingOrder()]);
    mockUpdateItem.mockImplementation(async (_id, _itemId, status) =>
      pendingOrder({
        items: [
          {
            id: 'item-1',
            productName: 'Schnitzel',
            quantity: 1,
            status: status as 'Preparing',
          },
        ],
      })
    );
  });

  it('renders three columns', async () => {
    await render(<KitchenDisplayScreen />);

    expect(await screen.findByTestId('kds-column-Pending')).toBeTruthy();
    expect(screen.getByTestId('kds-column-InPreparation')).toBeTruthy();
    expect(screen.getByTestId('kds-column-Ready')).toBeTruthy();
    expect(screen.getByLabelText('Offen')).toBeTruthy();
    expect(screen.getByLabelText('In Zubereitung')).toBeTruthy();
    expect(screen.getByLabelText('Fertig')).toBeTruthy();
  });

  it('shows table number and order age on the card', async () => {
    await render(<KitchenDisplayScreen />);

    expect(await screen.findByLabelText('Tisch 12')).toBeTruthy();
    expect(screen.getByTestId('kds-age')).toBeTruthy();
    expect(screen.getByText('00:00')).toBeTruthy();
    expect(screen.getByText('Ohne Zwiebel')).toBeTruthy();
  });

  it('toggles item status through the kitchen API', async () => {
    await render(<KitchenDisplayScreen />);
    await screen.findByLabelText('Tisch 12');

    await fireEvent.press(screen.getByLabelText('1 × Schnitzel'));

    await waitFor(() => {
      expect(mockUpdateItem).toHaveBeenCalledWith('ord-1', 'item-1', 'Preparing');
    });
  });

  it('inserts a new card from a SignalR created event without a full refresh', async () => {
    mockList.mockResolvedValue([]);
    await render(<KitchenDisplayScreen />);
    await screen.findByTestId('kds-columns');
    expect(screen.queryByLabelText('Tisch 7')).toBeNull();

    const listed = mockList.mock.calls.length;

    await act(async () => {
      hubOnCreated?.(
        pendingOrder({
          id: 'ord-live',
          tableNumber: '7',
          createdAtUtc: new Date().toISOString(),
        })
      );
    });

    expect(await screen.findByLabelText('Tisch 7')).toBeTruthy();
    expect(mockList.mock.calls.length).toBe(listed);
  });
});
