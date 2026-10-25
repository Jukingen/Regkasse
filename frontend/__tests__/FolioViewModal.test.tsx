import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { FolioViewModal } from '../components/lodging/FolioViewModal';
import { changeLanguage } from '../i18n';
import type { GuestFolioDto, RoomDto } from '../services/api/lodgingService';

const room: RoomDto = {
  id: 'room-2',
  number: '102',
  type: 'EZ',
  capacity: 1,
  status: 'Available',
  isActive: true,
  occupied: false,
};

function folio(open: boolean): GuestFolioDto {
  return {
    id: 'folio-1',
    customerId: 'cust-1',
    roomId: 'room-1',
    roomNumber: '101',
    checkIn: '2026-10-01T12:00:00Z',
    status: open ? 'Open' : 'Closed',
    balance: 12,
    isOpen: open,
  };
}

describe('FolioViewModal room move', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('shows Oda değiştir for an open folio', async () => {
    await render(
      <FolioViewModal
        visible
        roomNumber="101"
        folio={folio(true)}
        items={[]}
        availableRooms={[room]}
        busy={false}
        onClose={jest.fn()}
        onCheckOut={jest.fn()}
        onMoveRoom={jest.fn()}
      />
    );

    expect(screen.getByLabelText('Oda değiştir')).toBeTruthy();
  });

  it('hides Oda değiştir when the folio is not open', async () => {
    await render(
      <FolioViewModal
        visible
        roomNumber="101"
        folio={folio(false)}
        items={[]}
        availableRooms={[room]}
        busy={false}
        onClose={jest.fn()}
        onCheckOut={jest.fn()}
        onMoveRoom={jest.fn()}
      />
    );

    expect(screen.queryByLabelText('Oda değiştir')).toBeNull();
  });
});
