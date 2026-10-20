import { apiClient } from './config';

export type TicketEventSummary = {
  id: string;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  venue: string;
  capacity: number;
  isActive: boolean;
  stageEdge?: string | null;
};

export type TicketSeat = {
  id: string;
  eventId: string;
  row: string;
  number: string;
  section?: string | null;
  status: 'Available' | 'Reserved' | 'Sold' | string;
  reservedUntilUtc?: string | null;
  categoryId?: string | null;
  categoryCode?: string | null;
  categoryName?: string | null;
  gridColumn?: number | null;
  seatRole?: string | null;
  companionOfSeatId?: string | null;
  price?: number | null;
};

export type SoldSeatTicket = {
  redemptionId: string;
  code: string;
  displayCode: string;
  validUntilUtc?: string | null;
  eventName: string;
  seatLabel: string;
};

export const ticketEventService = {
  list: () => apiClient.get<TicketEventSummary[]>('/pos/ticket-events'),
  seats: (eventId: string) => apiClient.get<TicketSeat[]>(`/pos/ticket-events/${eventId}/seats`),
  reserve: (eventId: string, seatId: string) =>
    apiClient.post<TicketSeat>(`/pos/ticket-events/${eventId}/seats/reserve`, { seatId }),
  sell: (eventId: string, seatId: string) =>
    apiClient.post<SoldSeatTicket>(`/pos/ticket-events/${eventId}/seats/sell`, { seatId }),
};
