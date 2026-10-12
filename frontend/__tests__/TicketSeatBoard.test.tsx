import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen, waitFor } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React from 'react';

import { TicketSeatBoard } from '../components/TicketSeatBoard';
import { changeLanguage } from '../i18n';

const mockList = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockSeats = jest.fn<(...args: unknown[]) => Promise<unknown>>();

jest.mock('../services/api/ticketEventService', () => ({
  ticketEventService: {
    list: (...args: unknown[]) => mockList(...args),
    seats: (...args: unknown[]) => mockSeats(...args),
    reserve: jest.fn(),
    sell: jest.fn(),
  },
}));

describe('TicketSeatBoard', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockList.mockReset();
    mockSeats.mockReset();
    mockList.mockResolvedValue([
      {
        id: 'event-1',
        name: 'Kammerkonzert',
        startAtUtc: '2026-11-01T18:00:00Z',
        endAtUtc: '2026-11-01T20:00:00Z',
        venue: 'Saal 1',
        capacity: 20,
        isActive: true,
      },
    ]);
    mockSeats.mockResolvedValue([
      { id: 'seat-1', eventId: 'event-1', row: 'A', number: '12', section: 'Parkett', status: 'Available' },
      { id: 'seat-2', eventId: 'event-1', row: 'A', number: '13', section: 'Parkett', status: 'Sold' },
    ]);
  });

  it('renders the event picker and seat grid', async () => {
    const cashRegister = fs.readFileSync(
      path.join(__dirname, '../app/(tabs)/cash-register.tsx'),
      'utf8',
    );
    expect(cashRegister).toContain('<TicketSeatBoard');
    expect(cashRegister).toContain('feature="ticketScan"');

    await render(<TicketSeatBoard />);
    expect(await screen.findByTestId('ticket-seat-board')).toBeTruthy();
    expect(await screen.findByText('Kammerkonzert')).toBeTruthy();
    expect(await screen.findByTestId('ticket-seat-grid')).toBeTruthy();
    expect(screen.getByTestId('ticket-seat-A-12')).toBeTruthy();
    expect(screen.getByTestId('ticket-seat-A-13')).toBeTruthy();
    await waitFor(() => expect(mockSeats).toHaveBeenCalledWith('event-1'));
  });
});
