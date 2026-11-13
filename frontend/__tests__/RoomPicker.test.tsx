import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';
import { Alert } from 'react-native';

import { RoomPicker } from '../components/RoomPicker';
import { changeLanguage } from '../i18n';

const mockList = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockCreate = jest.fn<(...args: unknown[]) => Promise<unknown>>();
let mockProfile = { profileId: 'beherbergung' };

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => mockProfile,
}));

jest.mock('../services/api/lodgingService', () => ({
  listRooms: (...args: unknown[]) => mockList(...args),
  createFolio: (...args: unknown[]) => mockCreate(...args),
}));

describe('RoomPicker', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
    mockProfile = { profileId: 'beherbergung' };
    mockList.mockReset();
    mockCreate.mockReset();
    mockList.mockResolvedValue([
      {
        id: 'room-1',
        number: '101',
        type: 'DZ',
        capacity: 2,
        isActive: true,
        occupied: false,
      },
    ]);
    mockCreate.mockResolvedValue({
      id: 'folio-1',
      customerId: 'cust-1',
      roomId: 'room-1',
      isOpen: true,
    });
  });

  it('renders nothing when the profile is not beherbergung', async () => {
    mockProfile = { profileId: 'gastronomy' };
    await render(<RoomPicker customerId="cust-1" customerName="Anna" />);
    expect(screen.queryByLabelText('Zimmer wählen')).toBeNull();
  });

  it('lists rooms and opens a folio for beherbergung', async () => {
    const cashRegister = fs.readFileSync(
      path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
      'utf8'
    );
    expect(cashRegister).toContain("profileId === 'beherbergung'");
    expect(cashRegister).toContain('<RoomPicker');

    await render(<RoomPicker customerId="cust-1" customerName="Anna Gast" />);
    expect(await screen.findByLabelText('Zimmer wählen')).toBeTruthy();
    const row = await screen.findByLabelText('Zimmer 101 wählen');
    await fireEvent.press(row);
    await fireEvent.press(screen.getByLabelText('Check-in'));
    await waitFor(() =>
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({ customerId: 'cust-1', roomId: 'room-1' })
      )
    );
  });
});
