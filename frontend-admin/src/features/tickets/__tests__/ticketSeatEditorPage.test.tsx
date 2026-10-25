import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { TicketSeatEditorPage } from '@/features/tickets/TicketSeatEditorPage';
import { I18nProvider } from '@/i18n';
import { translateApiError } from '@/lib/api/errorTranslator';

const defaultSeats = [
  {
    id: 'seat-1',
    eventId: 'event-1',
    row: 'A',
    number: '1',
    status: 'Available',
    categoryName: 'VIP',
    gridColumn: 3,
    seatRole: 'Accessible',
  },
];

const { manage, createSeat, captured, seatRows } = vi.hoisted(() => ({
  manage: { current: true },
  createSeat: vi.fn(),
  captured: [] as unknown[],
  seatRows: {
    current: [
      {
        id: 'seat-1',
        eventId: 'event-1',
        row: 'A',
        number: '1',
        status: 'Available',
        categoryName: 'VIP',
        gridColumn: 3,
        seatRole: 'Accessible',
      },
    ] as Array<Record<string, unknown>>,
  },
}));

const event = {
  id: 'event-1',
  name: 'Kammerkonzert',
  venue: 'Saal 1',
  capacity: 20,
  isActive: true,
  startAtUtc: '2026-11-01T18:00:00.000Z',
  endAtUtc: '2026-11-01T20:00:00.000Z',
  stageEdge: 'top',
};

const categories = [
  { id: 'cat-vip', code: 'vip', name: 'VIP', sortOrder: 1, isActive: true },
  { id: 'cat-std', code: 'standard', name: 'Standard', sortOrder: 2, isActive: true },
  { id: 'cat-old', code: 'old', name: 'Archiv', sortOrder: 3, isActive: false },
];

const prices = [
  {
    id: 'price-1',
    eventId: 'event-1',
    categoryId: 'cat-vip',
    categoryName: 'VIP',
    price: 40,
    capacity: 10,
  },
];

vi.mock('@/api/admin/ticket-events', () => ({
  adminTicketEventQueryKeys: {
    all: ['admin', 'ticket-events'],
    seats: (id: string) => ['admin', 'ticket-events', id, 'seats'],
    prices: (id: string) => ['admin', 'ticket-events', id, 'category-prices'],
  },
  useAdminTicketEvents: () => ({ data: [event], isLoading: false, isError: false }),
  useAdminTicketSeats: () => ({
    data: seatRows.current,
    isLoading: false,
    isError: false,
  }),
  useAdminTicketCategoryPrices: () => ({ data: prices, isLoading: false, isError: false }),
  createAdminTicketSeat: (...args: unknown[]) => createSeat(...args),
  updateAdminTicketEvent: vi.fn(),
  setAdminTicketCategoryPrice: vi.fn(),
}));

vi.mock('@/api/admin/ticket-price-categories', () => ({
  useAdminTicketPriceCategories: () => ({ data: categories, isLoading: false, isError: false }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: (key: string) => key === 'product.view' || (key === 'product.manage' && manage.current),
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    apiError: (error: unknown) => {
      captured.push(error);
    },
  }),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <TicketSeatEditorPage eventId="event-1" />
        </App>
      </QueryClientProvider>
    </I18nProvider>
  );
}

describe('ticket seat editor', () => {
  beforeEach(() => {
    manage.current = true;
    captured.length = 0;
    createSeat.mockReset();
    seatRows.current = defaultSeats.map((row) => ({ ...row }));
  });

  it('shows one price row per active category and the seat fields', async () => {
    renderPage();

    expect(await screen.findByLabelText('Preis VIP')).toBeTruthy();
    expect(screen.getByLabelText('Preis Standard')).toBeTruthy();
    expect(screen.getByLabelText('Kapazität VIP')).toBeTruthy();
    expect(screen.getByLabelText('Kapazität Standard')).toBeTruthy();
    expect(screen.queryByLabelText('Preis Archiv')).toBeNull();

    expect(screen.getByText('Neuer Sitz')).toBeTruthy();
    expect(screen.getByLabelText('Kategorie')).toBeTruthy();
    expect(screen.getByLabelText('Spalte')).toBeTruthy();
    expect(screen.getByLabelText('Rolle')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Platz hinzufügen' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Speichern VIP' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Speichern Bühne' })).toBeTruthy();
    expect(screen.getByText('Rollstuhl')).toBeTruthy();
  });

  it('disables inputs for product.view', async () => {
    manage.current = false;
    renderPage();

    expect(await screen.findByLabelText('Preis VIP')).toBeDisabled();
    expect(screen.getByLabelText('Kapazität Standard')).toBeDisabled();
    expect(screen.getByLabelText('Kategorie')).toBeDisabled();
    expect(screen.getByLabelText('Spalte')).toBeDisabled();
    expect(screen.getByLabelText('Rolle')).toBeDisabled();
    expect(screen.getByLabelText('Bühne')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Platz hinzufügen' })).toBeNull();
    expect(screen.queryByRole('button', { name: /Speichern/ })).toBeNull();
  });

  it('maps SEAT_TAKEN and EVENT_CAPACITY_REACHED from a save', async () => {
    const user = userEvent.setup();
    createSeat
      .mockRejectedValueOnce({
        response: { status: 409, data: { code: 'SEAT_TAKEN', message: 'Seat is reserved.' } },
      })
      .mockRejectedValueOnce({
        response: {
          status: 409,
          data: { code: 'EVENT_CAPACITY_REACHED', message: 'Event is full.' },
        },
      });
    renderPage();

    await user.type(await screen.findByLabelText('Reihe'), 'A');
    await user.type(screen.getByLabelText('Nummer'), '1');
    await user.click(screen.getByRole('button', { name: 'Platz hinzufügen' }));
    await waitFor(() => expect(captured).toHaveLength(1));

    const t = (key: string) =>
      key === 'products.ticketEvents.seatTaken'
        ? 'Platz ist reserviert.'
        : key === 'products.ticketEvents.capacityReached'
          ? 'Die Kapazität ist erreicht.'
          : key;

    expect(translateApiError(t, captured[0], { skipLog: true })).toBe('Platz ist reserviert.');

    await user.click(screen.getByRole('button', { name: 'Platz hinzufügen' }));
    await waitFor(() => expect(captured).toHaveLength(2));
    expect(translateApiError(t, captured[1], { skipLog: true })).toBe('Die Kapazität ist erreicht.');
    expect(createSeat).toHaveBeenCalledTimes(2);
  });

  it('lists only unpaired accessible seats and shows the companion pairing', async () => {
    const user = userEvent.setup();
    seatRows.current = [
      {
        id: 'seat-free',
        eventId: 'event-1',
        row: 'A',
        number: '1',
        status: 'Available',
        categoryName: 'VIP',
        seatRole: 'Accessible',
      },
      {
        id: 'seat-main',
        eventId: 'event-1',
        row: 'A',
        number: '3',
        status: 'Sold',
        categoryName: 'VIP',
        seatRole: 'Accessible',
      },
      {
        id: 'seat-standard',
        eventId: 'event-1',
        row: 'C',
        number: '1',
        status: 'Available',
        seatRole: 'Standard',
      },
      {
        id: 'seat-companion',
        eventId: 'event-1',
        row: 'B',
        number: '2',
        status: 'Available',
        seatRole: 'Companion',
        companionOfSeatId: 'seat-main',
      },
      {
        id: 'seat-other-companion',
        eventId: 'event-1',
        row: 'D',
        number: '4',
        status: 'Available',
        seatRole: 'Companion',
      },
    ];
    renderPage();

    expect(await screen.findByText('Begleitung von Reihe A, Sitz 3')).toBeTruthy();

    await user.click(screen.getByLabelText('Begleiter von'));
    const listbox = await screen.findByRole('listbox');
    expect(within(listbox).getByRole('option', { name: 'Reihe A, Sitz 1' })).toBeTruthy();
    expect(within(listbox).queryByRole('option', { name: 'Reihe A, Sitz 3' })).toBeNull();
    expect(within(listbox).queryByRole('option', { name: 'Reihe B, Sitz 2' })).toBeNull();
    expect(within(listbox).queryByRole('option', { name: 'Reihe C, Sitz 1' })).toBeNull();
    expect(within(listbox).queryByRole('option', { name: 'Reihe D, Sitz 4' })).toBeNull();
  });
});
