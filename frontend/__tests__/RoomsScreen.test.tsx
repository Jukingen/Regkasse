import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';
import { Alert } from 'react-native';

import RoomsScreen from '../app/(screens)/rooms';
import { FolioChargeBar } from '../components/FolioChargeBar';
import { changeLanguage } from '../i18n';

const mockListRooms = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockListFolios = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockListItems = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockUpdate = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockCharge = jest.fn<(...args: unknown[]) => Promise<unknown>>();
let mockProfile = { profileId: 'beherbergung', posLayout: 'rooms' };

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => mockProfile,
}));

jest.mock('../services/api/lodgingService', () => ({
  listRooms: (...args: unknown[]) => mockListRooms(...args),
  listFolios: (...args: unknown[]) => mockListFolios(...args),
  listFolioItems: (...args: unknown[]) => mockListItems(...args),
  updateFolio: (...args: unknown[]) => mockUpdate(...args),
  chargeFolio: (...args: unknown[]) => mockCharge(...args),
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

  it('posts a folio charge instead of a payment', async () => {
    mockListFolios.mockResolvedValue([
      {
        id: 'folio-1',
        customerName: 'Anna Gast',
        roomNumber: '101',
        balance: 0,
        isOpen: true,
        status: 'Open',
      },
    ]);
    await render(<FolioChargeBar amount={18} description="Abendessen" />);
    await fireEvent.press(await screen.findByLabelText('Konto Zimmer 101'));
    await fireEvent.press(screen.getByLabelText('Auf Zimmer buchen'));
    await waitFor(() =>
      expect(mockCharge).toHaveBeenCalledWith('folio-1', {
        description: 'Abendessen',
        amount: 18,
      })
    );
  });
});
