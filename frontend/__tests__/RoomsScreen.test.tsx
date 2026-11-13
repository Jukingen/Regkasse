import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';
import { Alert, StyleSheet } from 'react-native';

import RoomsScreen from '../app/(screens)/rooms';
import { FolioChargeBar } from '../components/FolioChargeBar';
import { takeFolioCheckout } from '../components/lodging/folioCheckoutHandoff';
import { ROOM_STATUS_BG } from '../components/lodging/roomStatusColors';
import { changeLanguage } from '../i18n';

const mockListRooms = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockListFolios = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockListItems = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockUpdate = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockCharge = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockCreateFolio = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockUpdateRoom = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockStage = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockProducts = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockPush = jest.fn();
const mockLookup = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockCreateCustomer = jest.fn<(...args: unknown[]) => Promise<unknown>>();
let mockProfile = { profileId: 'beherbergung', posLayout: 'rooms' };

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => mockProfile,
}));

jest.mock('expo-router', () => ({
  useFocusEffect: (effect: () => void | (() => void)) => {
    const React = require('react') as typeof import('react');
    React.useEffect(() => effect(), [effect]);
  },
  useRouter: () => ({ push: mockPush }),
}));

jest.mock('../services/api/productService', () => ({
  getAllProducts: (...args: unknown[]) => mockProducts(...args),
}));

jest.mock('../services/api/customerService', () => ({
  customerService: {
    lookupByQrPayload: (...args: unknown[]) => mockLookup(...args),
    create: (...args: unknown[]) => mockCreateCustomer(...args),
  },
}));

jest.mock('../services/api/lodgingService', () => ({
  listRooms: (...args: unknown[]) => mockListRooms(...args),
  listFolios: (...args: unknown[]) => mockListFolios(...args),
  listFolioItems: (...args: unknown[]) => mockListItems(...args),
  updateFolio: (...args: unknown[]) => mockUpdate(...args),
  chargeFolio: (...args: unknown[]) => mockCharge(...args),
  stageCheckout: (...args: unknown[]) => mockStage(...args),
  createFolio: (...args: unknown[]) => mockCreateFolio(...args),
  updateRoomStatus: (...args: unknown[]) => mockUpdateRoom(...args),
}));

describe('rooms screen', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
    mockProfile = { profileId: 'beherbergung', posLayout: 'rooms' };
    mockListRooms.mockReset();
    mockListFolios.mockReset();
    mockListItems.mockReset();
    mockUpdate.mockReset();
    mockCharge.mockReset();
    mockCreateFolio.mockReset();
    mockUpdateRoom.mockReset();
    mockStage.mockReset();
    mockProducts.mockReset();
    mockPush.mockReset();
    mockLookup.mockReset();
    mockCreateCustomer.mockReset();
    takeFolioCheckout();
    mockProducts.mockResolvedValue([]);
    mockStage.mockResolvedValue({
      lineName: 'Zimmer 101 — Aufenthalt',
      balance: 12.5,
      productId: 'prod-stay',
      tableNumber: 1,
    });
    mockUpdate.mockResolvedValue({ id: 'folio-1', status: 'Closed', balance: 12.5 });
    mockCharge.mockResolvedValue({ id: 'folio-1', balance: 18, isOpen: true });
    mockListRooms.mockResolvedValue([
      {
        id: 'room-1',
        number: '101',
        type: 'DZ',
        capacity: 2,
        status: 'Occupied',
        isActive: true,
        occupied: true,
      },
    ]);
    mockListFolios.mockResolvedValue([
      {
        id: 'folio-1',
        customerId: 'cust-1',
        customerName: 'Anna Gast',
        roomId: 'room-1',
        roomNumber: '101',
        checkIn: '2026-10-01T12:00:00Z',
        status: 'Open',
        balance: 12.5,
        isOpen: true,
      },
    ]);
    mockListItems.mockResolvedValue([
      {
        id: 'item-1',
        folioId: 'folio-1',
        description: 'Frühstück',
        amount: 12.5,
        createdAtUtc: '2026-10-01T13:00:00Z',
      },
    ]);
  });

  it('renders the room grid and folio charges', async () => {
    await render(<RoomsScreen />);
    expect(await screen.findByLabelText('Zimmer')).toBeTruthy();
    await fireEvent.press(await screen.findByLabelText('Zimmer 101, Belegt'));
    expect(await screen.findByLabelText('Buchung Frühstück')).toBeTruthy();
    expect(screen.getByText('Anna Gast · 12.50')).toBeTruthy();
  });

  it('colors the room grid by status', async () => {
    mockListRooms.mockResolvedValue([
      { id: 'room-free', number: '102', type: 'EZ', capacity: 1, status: 'Available', isActive: true, occupied: false },
      { id: 'room-busy', number: '101', type: 'DZ', capacity: 2, status: 'Occupied', isActive: true, occupied: true },
      { id: 'room-clean', number: '103', type: 'EZ', capacity: 1, status: 'Cleaning', isActive: true, occupied: false },
      { id: 'room-fix', number: '104', type: 'EZ', capacity: 1, status: 'Maintenance', isActive: true, occupied: false },
    ]);
    await render(<RoomsScreen />);
    await screen.findByLabelText('Zimmer 102, Frei');
    const background = (label: string) =>
      StyleSheet.flatten(screen.getByLabelText(label).props.style).backgroundColor;
    expect(background('Zimmer 102, Frei')).toBe(ROOM_STATUS_BG.Available);
    expect(background('Zimmer 101, Belegt')).toBe(ROOM_STATUS_BG.Occupied);
    expect(background('Zimmer 103, Reinigung')).toBe(ROOM_STATUS_BG.Cleaning);
    expect(background('Zimmer 104, Wartung')).toBe(ROOM_STATUS_BG.Maintenance);
  });

  it('posts a folio charge instead of a payment and clears the cart', async () => {
    const onCharged = jest.fn();
    mockListFolios.mockResolvedValue([
      {
        id: 'folio-1',
        customerId: 'cust-anna',
        customerName: 'Anna Gast',
        roomNumber: '101',
        balance: 0,
        isOpen: true,
        status: 'Open',
      },
    ]);
    await render(
      <FolioChargeBar
        amount={18}
        description="Abendessen"
        customerId="cust-anna"
        onCharged={onCharged}
      />
    );
    await fireEvent.press(await screen.findByLabelText('Konto Zimmer 101'));
    await fireEvent.press(screen.getByLabelText('Auf Zimmer buchen'));
    await waitFor(() =>
      expect(mockCharge).toHaveBeenCalledWith('folio-1', {
        description: 'Abendessen',
        amount: 18,
      })
    );
    expect(onCharged).toHaveBeenCalled();
  });

  it('hides charge-to-room when the sale customer has no open folio', async () => {
    mockListFolios.mockResolvedValue([
      {
        id: 'folio-1',
        customerId: 'cust-anna',
        customerName: 'Anna Gast',
        roomNumber: '101',
        balance: 4,
        isOpen: true,
        status: 'Open',
      },
    ]);
    await render(<FolioChargeBar amount={18} customerId="someone-else" />);
    await waitFor(() => expect(mockListFolios).toHaveBeenCalled());
    expect(screen.queryByLabelText('Auf Zimmer buchen')).toBeNull();
  });

  it('shows the folio and closes it on check-out', async () => {
    await render(<RoomsScreen />);
    await fireEvent.press(await screen.findByLabelText('Zimmer 101, Belegt'));
    await fireEvent.press(await screen.findByLabelText('Konto anzeigen'));
    expect(await screen.findByLabelText(/Frühstück · 12\.50/)).toBeTruthy();
    await fireEvent.press(screen.getByLabelText('Check-out'));
    await fireEvent.press(await screen.findByLabelText('Check-out bestätigen'));
    await waitFor(() => expect(mockUpdate).toHaveBeenCalledWith('folio-1', { status: 'Closed' }));
  });

  it('stages a stay line for the register without closing the folio', async () => {
    mockProducts.mockResolvedValue([
      {
        id: 'prod-stay',
        isActive: true,
        imeiTracked: false,
        categoryId: 'cat-1',
        stockQuantity: 5,
        taxType: 'Special',
      },
    ]);
    await render(<RoomsScreen />);
    await fireEvent.press(await screen.findByLabelText('Zimmer 101, Belegt'));
    await fireEvent.press(await screen.findByLabelText('Check-out'));
    await fireEvent.press(await screen.findByLabelText('Zur Kasse'));
    await waitFor(() => expect(mockStage).toHaveBeenCalledWith('folio-1', 'prod-stay'));
    expect(mockUpdate).not.toHaveBeenCalled();
    expect(mockPush).toHaveBeenCalledWith('/(tabs)/cash-register');
    expect(takeFolioCheckout()).toMatchObject({ folioId: 'folio-1', tableNumber: 1, balance: 12.5 });
  });

  it('checks a guest in and opens a folio', async () => {
    mockListRooms.mockResolvedValue([
      {
        id: 'room-free',
        number: '102',
        type: 'EZ',
        capacity: 1,
        status: 'Available',
        isActive: true,
        occupied: false,
      },
    ]);
    mockListFolios.mockReset();
    mockListFolios
      .mockResolvedValueOnce([])
      .mockResolvedValue([
        {
          id: 'folio-9',
          customerId: 'cust-9',
          customerName: 'Max Gast',
          roomId: 'room-free',
          status: 'Open',
          balance: 0,
          isOpen: true,
        },
      ]);
    mockLookup.mockResolvedValue({ id: 'cust-9', name: 'Max Gast' });
    mockCreateFolio.mockResolvedValue({
      id: 'folio-9',
      customerId: 'cust-9',
      customerName: 'Max Gast',
      roomId: 'room-free',
      status: 'Open',
      balance: 0,
      isOpen: true,
    });

    await render(<RoomsScreen />);
    await fireEvent.press(await screen.findByLabelText('Zimmer 102, Frei'));
    await waitFor(() => expect(mockListFolios).toHaveBeenCalled());
    await fireEvent.press(await screen.findByLabelText('Check-in Zimmer 102'));
    fireEvent.changeText(screen.getByLabelText('Kundennummer oder E-Mail'), 'max@hotel.test');
    await waitFor(() =>
      expect(screen.getByLabelText('Kundennummer oder E-Mail').props.value).toBe('max@hotel.test')
    );
    await fireEvent.press(screen.getByLabelText('Gast suchen'));
    expect(await screen.findByText('Gast: Max Gast')).toBeTruthy();
    fireEvent.changeText(screen.getByLabelText('Nächte (optional)'), '2');
    await fireEvent.press(screen.getByLabelText('Zimmer 102 wählen'));
    await fireEvent.press(screen.getByLabelText('Check-in speichern'));

    await waitFor(() => expect(mockCreateFolio).toHaveBeenCalledTimes(1));
    const body = mockCreateFolio.mock.calls[0][0] as {
      customerId: string;
      roomId: string;
      checkIn: string;
      checkOut: string;
    };
    expect(body.customerId).toBe('cust-9');
    expect(body.roomId).toBe('room-free');
    expect(body.checkIn).toMatch(/T12:00:00.000Z$/);
    expect(new Date(body.checkOut).getTime() - new Date(body.checkIn).getTime()).toBe(2 * 24 * 60 * 60 * 1000);
    expect(await screen.findByText('Max Gast · 0.00')).toBeTruthy();
    expect(mockListRooms).toHaveBeenCalled();
  });
});
