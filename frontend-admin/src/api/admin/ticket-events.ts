import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';
import { queryCacheDynamic } from '@/lib/query/queryCachePolicy';

export interface AdminTicketEvent {
  id: string;
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  venue: string;
  capacity: number;
  isActive: boolean;
  stageEdge?: string | null;
}

export interface AdminTicketSeat {
  id: string;
  eventId: string;
  row: string;
  number: string;
  section?: string | null;
  status: string;
  reservedUntilUtc?: string | null;
  categoryId?: string | null;
  categoryCode?: string | null;
  categoryName?: string | null;
  gridColumn?: number | null;
  seatRole?: string | null;
  companionOfSeatId?: string | null;
  price?: number | null;
}

export interface AdminTicketCategoryPrice {
  id: string;
  eventId: string;
  categoryId: string;
  categoryCode?: string | null;
  categoryName?: string | null;
  price: number;
  capacity: number;
}

export interface UpsertTicketEventBody {
  name: string;
  startAtUtc: string;
  endAtUtc: string;
  venue: string;
  capacity: number;
  isActive: boolean;
  stageEdge?: string | null;
}

export interface CreateTicketSeatBody {
  row: string;
  number: string;
  section?: string;
  categoryId?: string;
  gridColumn?: number;
  seatRole?: number;
  companionOfSeatId?: string;
}

export interface SetTicketCategoryPriceBody {
  categoryId: string;
  price: number;
  capacity: number;
}

export const adminTicketEventQueryKeys = {
  all: ['admin', 'ticket-events'] as const,
  seats: (eventId: string) => ['admin', 'ticket-events', eventId, 'seats'] as const,
  prices: (eventId: string) => ['admin', 'ticket-events', eventId, 'category-prices'] as const,
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
    ...queryCacheDynamic,
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
    ...queryCacheDynamic,
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

export function getAdminTicketCategoryPrices(
  eventId: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminTicketCategoryPrice[]> {
  return customInstance<AdminTicketCategoryPrice[]>(
    { url: `/api/admin/ticket-events/${eventId}/category-prices`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminTicketCategoryPrice[]>(res));
}

export function useAdminTicketCategoryPrices(
  eventId: string,
  options?: Partial<UseQueryOptions<AdminTicketCategoryPrice[], Error, AdminTicketCategoryPrice[]>>
): UseQueryResult<AdminTicketCategoryPrice[], Error> {
  return useQuery({
    queryKey: adminTicketEventQueryKeys.prices(eventId),
    queryFn: ({ signal }) => getAdminTicketCategoryPrices(eventId, undefined, signal),
    enabled: Boolean(eventId),
    ...queryCacheDynamic,
    ...options,
  });
}

export function setAdminTicketCategoryPrice(
  eventId: string,
  body: SetTicketCategoryPriceBody
): Promise<AdminTicketCategoryPrice> {
  return customInstance<AdminTicketCategoryPrice>({
    url: `/api/admin/ticket-events/${eventId}/category-prices`,
    method: 'PUT',
    data: body,
  }).then((res) => unwrapData<AdminTicketCategoryPrice>(res));
}
