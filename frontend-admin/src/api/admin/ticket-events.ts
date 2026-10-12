import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export interface AdminTicketEvent {
  id: string;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  venue: string;
  capacity: number;
  isActive: boolean;
}

export interface AdminTicketSeat {
  id: string;
  eventId: string;
  row: string;
  number: string;
  section?: string | null;
  status: string;
  reservedUntilUtc?: string | null;
}

export interface UpsertTicketEventBody {
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  venue: string;
  capacity: number;
  isActive: boolean;
}

export interface CreateTicketSeatBody {
  row: string;
  number: string;
  section?: string;
}

export const adminTicketEventQueryKeys = {
  all: ['admin', 'ticket-events'] as const,
  seats: (eventId: string) => ['admin', 'ticket-events', eventId, 'seats'] as const,
};

export function getAdminTicketEvents(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminTicketEvent[]> {
  return customInstance<AdminTicketEvent[]>(
    { url: '/api/admin/ticket-events', method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminTicketEvent[]>(res));
}

export function useAdminTicketEvents(
  options?: Partial<UseQueryOptions<AdminTicketEvent[], Error, AdminTicketEvent[]>>
): UseQueryResult<AdminTicketEvent[], Error> {
  return useQuery({
    queryKey: adminTicketEventQueryKeys.all,
    queryFn: ({ signal }) => getAdminTicketEvents(undefined, signal),
    ...options,
  });
}

export function createAdminTicketEvent(body: UpsertTicketEventBody): Promise<AdminTicketEvent> {
  return customInstance<AdminTicketEvent>({
    url: '/api/admin/ticket-events',
    method: 'POST',
    data: body,
  }).then((res) => unwrapData<AdminTicketEvent>(res));
}

export function updateAdminTicketEvent(
  id: string,
  body: UpsertTicketEventBody
): Promise<AdminTicketEvent> {
  return customInstance<AdminTicketEvent>({
    url: `/api/admin/ticket-events/${id}`,
    method: 'PUT',
    data: body,
  }).then((res) => unwrapData<AdminTicketEvent>(res));
}

export function getAdminTicketSeats(
  eventId: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminTicketSeat[]> {
  return customInstance<AdminTicketSeat[]>(
    { url: `/api/admin/ticket-events/${eventId}/seats`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminTicketSeat[]>(res));
}

export function useAdminTicketSeats(
  eventId: string,
  options?: Partial<UseQueryOptions<AdminTicketSeat[], Error, AdminTicketSeat[]>>
): UseQueryResult<AdminTicketSeat[], Error> {
  return useQuery({
    queryKey: adminTicketEventQueryKeys.seats(eventId),
    queryFn: ({ signal }) => getAdminTicketSeats(eventId, undefined, signal),
    enabled: Boolean(eventId),
    ...options,
  });
}

export function createAdminTicketSeat(
  eventId: string,
  body: CreateTicketSeatBody
): Promise<AdminTicketSeat> {
  return customInstance<AdminTicketSeat>({
    url: `/api/admin/ticket-events/${eventId}/seats`,
    method: 'POST',
    data: body,
  }).then((res) => unwrapData<AdminTicketSeat>(res));
}
