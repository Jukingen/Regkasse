import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react-native';
import fs from 'fs';
import path from 'path';
import React, { useState } from 'react';
import { Text } from 'react-native';

import { TicketSeatBoard } from '../components/TicketSeatBoard';
import { ticketSeatToastMessage } from '../components/ticketSeatNotice';
import { Colors } from '../constants/Colors';
import { SoftColors } from '../constants/SoftTheme';
import i18n, { changeLanguage } from '../i18n';

const mockList = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockSeats = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockReserve = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockSell = jest.fn<(...args: unknown[]) => Promise<unknown>>();

jest.mock('../services/api/ticketEventService', () => ({
  ticketEventService: {
    list: (...args: unknown[]) => mockList(...args),
    seats: (...args: unknown[]) => mockSeats(...args),
    reserve: (...args: unknown[]) => mockReserve(...args),
    sell: (...args: unknown[]) => mockSell(...args),
  },
}));

const event = {
  id: 'event-1',
  name: 'Kammerkonzert',
  startAtUtc: '2026-11-01T18:00:00Z',
  endAtUtc: '2026-11-01T20:00:00Z',
  venue: 'Saal 1',
  capacity: 20,
  isActive: true,
  stageEdge: 'top',
};

type TestNode = { props?: { testID?: unknown }; children?: unknown[] };

function testIds(node: TestNode): string[] {
  const own = typeof node.props?.testID === 'string' ? [node.props.testID] : [];
  const children = node.children ?? [];
  return own.concat(
    children.flatMap((child) =>
      typeof child === 'string' || child == null ? [] : testIds(child as TestNode),
    ),
  );
}

function NotifyHarness() {
  const [toast, setToast] = useState('');
  const [cart] = useState('Espresso');
  return (
    <>
      <TicketSeatBoard
        onError={(notice) => {
          setToast(ticketSeatToastMessage((key) => i18n.t(`verticalProfiles:${key}`), notice));
        }}
      />
      <Text testID="seat-toast">{toast}</Text>
      <Text testID="sale-cart">{cart}</Text>
    </>
  );
}

describe('TicketSeatBoard', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockList.mockReset();
    mockSeats.mockReset();
    mockReserve.mockReset();
    mockSell.mockReset();
    mockReserve.mockResolvedValue({});
    mockSell.mockResolvedValue({
      redemptionId: 'red-1',
      code: 'TKT-PLAINTEXT01',
      displayCode: 'TKT-PLAIN',
      eventName: 'Kammerkonzert',
      seatLabel: 'A1',
    });
    mockList.mockResolvedValue([event]);
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
    const boardStart = cashRegister.indexOf('<TicketSeatBoard');
    const boardEnd = cashRegister.indexOf('/>', boardStart);
    const boardJsx = cashRegister.slice(boardStart, boardEnd);
    expect(boardJsx).toContain('ticketSeatToastMessage');
    expect(boardJsx).not.toContain('clearCart');

    await render(<TicketSeatBoard />);
    expect(await screen.findByTestId('ticket-seat-board')).toBeTruthy();
    expect(await screen.findByText('Kammerkonzert')).toBeTruthy();
    expect(await screen.findByTestId('ticket-seat-grid')).toBeTruthy();
    expect(screen.getByTestId('ticket-seat-A-12')).toBeTruthy();
    expect(screen.getByTestId('ticket-seat-A-13')).toBeTruthy();
    await waitFor(() => expect(mockSeats).toHaveBeenCalledWith('event-1'));
  });

  it('lays out rows, an aisle, role labels, and category colors', async () => {
    mockSeats.mockResolvedValue([
      {
        id: 'a1',
        eventId: 'event-1',
        row: 'A',
        number: '1',
        status: 'Available',
        gridColumn: 1,
        categoryCode: 'standard',
        categoryId: 'cat-std',
        price: 20,
        seatRole: 'Standard',
      },
      {
        id: 'a3',
        eventId: 'event-1',
        row: 'A',
        number: '3',
        status: 'Available',
        gridColumn: 3,
        categoryCode: 'vip',
        categoryId: 'cat-vip',
        price: 40,
        seatRole: 'Standard',
      },
      {
        id: 'b1',
        eventId: 'event-1',
        row: 'B',
        number: '1',
        status: 'Available',
        gridColumn: 1,
        categoryCode: 'standard',
        seatRole: 'Accessible',
        price: 20,
      },
      {
        id: 'b2',
        eventId: 'event-1',
        row: 'B',
        number: '2',
        status: 'Available',
        gridColumn: 2,
        categoryCode: 'standard',
        seatRole: 'Companion',
        price: 20,
      },
      {
        id: 'b3',
        eventId: 'event-1',
        row: 'B',
        number: '3',
        status: 'Available',
        gridColumn: 3,
        categoryCode: 'student',
        seatRole: 'Standard',
        price: 12,
      },
    ]);

    await render(<TicketSeatBoard />);
    const grid = await screen.findByTestId('ticket-seat-grid');
    const ids = testIds(grid);
    expect(ids.indexOf('ticket-stage-label')).toBeLessThan(ids.indexOf('ticket-seat-row-A'));

    const rowA = within(screen.getByTestId('ticket-seat-row-A'));
    const rowAIds = testIds(screen.getByTestId('ticket-seat-row-A'));
    expect(rowAIds).toEqual(['ticket-seat-row-A', 'ticket-seat-A-1', 'ticket-seat-aisle', 'ticket-seat-A-3']);
    expect(rowA.getByLabelText('Gang')).toBeTruthy();
    expect(screen.getByText(/Rollstuhl/)).toBeTruthy();
    expect(screen.getByText('Begleitung')).toBeTruthy();
    expect(screen.getByText('40,00 €')).toBeTruthy();

    expect(screen.getByTestId('ticket-seat-A-1')).toHaveStyle({ backgroundColor: SoftColors.bgSecondary });
    expect(screen.getByTestId('ticket-seat-A-3')).toHaveStyle({ backgroundColor: SoftColors.warning });
    expect(screen.getByTestId('ticket-seat-B-1')).toHaveStyle({
      backgroundColor: SoftColors.bgSecondary,
      borderColor: SoftColors.focusRing,
    });
    expect(screen.getByTestId('ticket-seat-B-2')).toHaveStyle({
      backgroundColor: SoftColors.bgSecondary,
      borderColor: Colors.accent,
    });
    expect(screen.getByTestId('ticket-seat-B-3')).toHaveStyle({ backgroundColor: SoftColors.info });
  });

  it('places the stage label at the bottom when stageEdge is bottom', async () => {
    mockList.mockResolvedValue([{ ...event, stageEdge: 'bottom' }]);
    await render(<TicketSeatBoard />);
    const grid = await screen.findByTestId('ticket-seat-grid');
    const ids = testIds(grid);
    expect(ids.indexOf('ticket-seat-row-A')).toBeLessThan(ids.indexOf('ticket-stage-label'));
    expect(screen.getByText('Bühne')).toBeTruthy();
  });

  it('shows the empty copy when the event has no seats', async () => {
    mockSeats.mockResolvedValue([]);
    await render(<TicketSeatBoard />);
    expect(await screen.findByText('Keine Plätze vorhanden.')).toBeTruthy();
  });

  it('shows the hold message on SEAT_TAKEN and keeps the sale cart', async () => {
    mockSeats.mockResolvedValue([
      { id: 'seat-1', eventId: 'event-1', row: 'A', number: '1', status: 'Available' },
      { id: 'seat-2', eventId: 'event-1', row: 'A', number: '2', status: 'Available' },
    ]);
    mockSell.mockRejectedValue({ status: 409, data: { code: 'SEAT_TAKEN', message: 'held' } });

    await render(<NotifyHarness />);
    await fireEvent.press(await screen.findByTestId('ticket-seat-A-1'));
    await fireEvent.press(await screen.findByTestId('ticket-seat-pay'));

    expect(await screen.findByText('Platz ist für kurze Zeit reserviert. Bitte anderen Platz wählen.')).toBeTruthy();
    expect(screen.getByText('Espresso')).toBeTruthy();
    expect(mockSell).toHaveBeenCalledTimes(1);

    await fireEvent.press(screen.getByTestId('ticket-seat-A-2'));
    await waitFor(() => expect(mockReserve).toHaveBeenCalledWith('event-1', 'seat-2'));
    expect(mockSell).toHaveBeenCalledTimes(1);
  });

  it('shows the issued ticket code after a successful sell', async () => {
    mockSeats.mockResolvedValue([
      { id: 'seat-1', eventId: 'event-1', row: 'A', number: '1', status: 'Available' },
    ]);
    await render(<TicketSeatBoard />);
    await fireEvent.press(await screen.findByTestId('ticket-seat-A-1'));
    await fireEvent.press(await screen.findByTestId('ticket-seat-pay'));
    expect(await screen.findByTestId('ticket-print-code')).toHaveTextContent('TKT-PLAINTEXT01');
    expect(mockSell).toHaveBeenCalledTimes(1);
  });

  it('surfaces category capacity and missing price without clearing the cart', async () => {
    expect(
      ticketSeatToastMessage((key) => i18n.t(`verticalProfiles:${key}`), 'CATEGORY_PRICE_MISSING'),
    ).toBe('Für diese Kategorie ist kein Preis hinterlegt.');

    mockSeats.mockResolvedValue([
      { id: 'seat-1', eventId: 'event-1', row: 'A', number: '1', status: 'Available' },
      { id: 'seat-2', eventId: 'event-1', row: 'A', number: '2', status: 'Available' },
    ]);
    mockSell.mockRejectedValue({
      status: 409,
      data: { code: 'CATEGORY_CAPACITY_REACHED', message: 'full' },
    });

    await render(<NotifyHarness />);
    await fireEvent.press(await screen.findByTestId('ticket-seat-A-1'));
    await fireEvent.press(await screen.findByTestId('ticket-seat-pay'));

    expect(await screen.findByText('Diese Kategorie ist ausverkauft. Bitte anderen Platz wählen.')).toBeTruthy();
    expect(screen.getByText('Espresso')).toBeTruthy();
    expect(mockSell).toHaveBeenCalledTimes(1);
  });

  it('toasts and does not sell a companion seat before its target', async () => {
    mockSeats.mockResolvedValue([
      {
        id: 'seat-main',
        eventId: 'event-1',
        row: 'A',
        number: '3',
        status: 'Available',
        seatRole: 'Accessible',
      },
      {
        id: 'seat-companion',
        eventId: 'event-1',
        row: 'A',
        number: '4',
        status: 'Available',
        seatRole: 'Companion',
        companionOfSeatId: 'seat-main',
      },
    ]);

    await render(<NotifyHarness />);
    expect(await screen.findByText('↳ Begleitung von A, Sitz 3')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('ticket-seat-A-4'));

    expect(
      await screen.findByText('Begleiterplatz kann erst nach dem Hauptplatz verkauft werden.'),
    ).toBeTruthy();
    expect(mockSell).not.toHaveBeenCalled();
    expect(mockReserve).not.toHaveBeenCalled();
  });
});
