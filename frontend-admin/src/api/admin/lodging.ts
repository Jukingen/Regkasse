import type { UseMutationResult, UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export interface AdminRoomRow {
  id: string;
  number: string;
  type: string;
  capacity: number;
  status?: string;
  isActive: boolean;
  occupied: boolean;
}

export interface AdminGuestFolioRow {
  id: string;
  customerId: string;
  customerName?: string | null;
  roomId: string;
  roomNumber?: string | null;
  checkIn: string;
  checkOut?: string | null;
  status?: string;
  balance: number;
  notes?: string | null;
  isOpen: boolean;
}

export type CreateAdminRoomRequest = {
  number: string;
  type: string;
  capacity: number;
};

export type AdminFolioListQuery = {
  openOnly?: boolean;
  fromUtc?: string;
  toUtc?: string;
};

export interface AdminFolioItem {
  id: string;
  folioId: string;
  description: string;
  amount: number;
  createdAtUtc: string;
  paymentDetailId?: string | null;
}

export type UpdateAdminRoomDetailsRequest = CreateAdminRoomRequest;

export type ChargeAdminFolioRequest = {
  description: string;
  amount: number;
};

export const adminLodgingQueryKeys = {
  all: ['admin', 'lodging'] as const,
  rooms: () => [...adminLodgingQueryKeys.all, 'rooms'] as const,
  room: (id: string) => [...adminLodgingQueryKeys.rooms(), id] as const,
  folios: (query?: AdminFolioListQuery) => [...adminLodgingQueryKeys.all, 'folios', query ?? {}] as const,
  folio: (id: string) => [...adminLodgingQueryKeys.all, 'folio', id] as const,
  folioItems: (id: string) => [...adminLodgingQueryKeys.folio(id), 'items'] as const,
};

function folioSearch(query?: AdminFolioListQuery): string {
  const params = new URLSearchParams();
  if (query?.openOnly) params.set('openOnly', 'true');
  if (query?.fromUtc) params.set('fromUtc', query.fromUtc);
  if (query?.toUtc) params.set('toUtc', query.toUtc);
  const text = params.toString();
  return text ? `?${text}` : '';
}

export function getAdminRooms(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminRoomRow[]> {
  return customInstance<AdminRoomRow[]>({ url: '/api/admin/rooms', method: 'GET', signal }, options).then(
    (res) => unwrapData<AdminRoomRow[]>(res)
  );
}

export function getAdminRoom(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminRoomRow> {
  return customInstance<AdminRoomRow>(
    { url: `/api/admin/rooms/${id}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminRoomRow>(res));
}

export function createAdminRoom(
  body: CreateAdminRoomRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminRoomRow> {
  return customInstance<AdminRoomRow>(
    { url: '/api/admin/rooms', method: 'POST', data: body },
    options
  ).then((res) => unwrapData<AdminRoomRow>(res));
}

export function updateAdminRoomStatus(
  id: string,
  status: string,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminRoomRow> {
  return customInstance<AdminRoomRow>(
    { url: `/api/admin/rooms/${id}`, method: 'PATCH', data: { status } },
    options
  ).then((res) => unwrapData<AdminRoomRow>(res));
}

export function updateAdminRoomDetails(
  id: string,
  body: UpdateAdminRoomDetailsRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminRoomRow> {
  return customInstance<AdminRoomRow>(
    { url: `/api/admin/rooms/${id}/details`, method: 'PATCH', data: body },
    options
  ).then((res) => unwrapData<AdminRoomRow>(res));
}

export function deleteAdminRoom(
  id: string,
  options?: SecondParameter<typeof customInstance>
): Promise<void> {
  return customInstance<void>({ url: `/api/admin/rooms/${id}`, method: 'DELETE' }, options).then(
    () => undefined
  );
}

export function getAdminFolios(
  query?: AdminFolioListQuery,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminGuestFolioRow[]> {
  return customInstance<AdminGuestFolioRow[]>(
    { url: `/api/admin/folios${folioSearch(query)}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow[]>(res));
}

export function getAdminFolio(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminGuestFolioRow> {
  return customInstance<AdminGuestFolioRow>(
    { url: `/api/admin/folios/${id}`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow>(res));
}

export function getAdminFolioItems(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminFolioItem[]> {
  return customInstance<AdminFolioItem[]>(
    { url: `/api/admin/folios/${id}/items`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminFolioItem[]>(res));
}

export function chargeAdminFolio(
  id: string,
  body: ChargeAdminFolioRequest,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminGuestFolioRow> {
  return customInstance<AdminGuestFolioRow>(
    { url: `/api/admin/folios/${id}/charge`, method: 'POST', data: body },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow>(res));
}

export function moveAdminFolio(
  id: string,
  newRoomId: string,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminGuestFolioRow> {
  return customInstance<AdminGuestFolioRow>(
    { url: `/api/admin/folios/${id}/move-room`, method: 'POST', data: { newRoomId } },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow>(res));
}

export function checkoutAdminFolio(
  id: string,
  options?: SecondParameter<typeof customInstance>
): Promise<AdminGuestFolioRow> {
  return customInstance<AdminGuestFolioRow>(
    { url: `/api/admin/folios/${id}`, method: 'PATCH', data: { status: 'Closed' } },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow>(res));
}

export function useAdminRooms(
  options?: Partial<UseQueryOptions<AdminRoomRow[], Error, AdminRoomRow[]>>
): UseQueryResult<AdminRoomRow[], Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.rooms(),
    queryFn: ({ signal }) => getAdminRooms(undefined, signal),
    ...options,
  });
}

export interface AdminRoomMinibarRow {
  id: string;
  roomId: string;
  productId?: string | null;
  description: string;
  quantity: number;
  unitPrice: number;
  status: string;
  chargedFolioId?: string | null;
  chargedAtUtc?: string | null;
  createdAtUtc: string;
}

export function getAdminRoomMinibar(
  id: string,
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminRoomMinibarRow[]> {
  return customInstance<AdminRoomMinibarRow[]>(
    { url: `/api/admin/rooms/${id}/minibar`, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminRoomMinibarRow[]>(res));
}

export function useAdminRoomMinibar(
  id: string,
  options?: Partial<UseQueryOptions<AdminRoomMinibarRow[], Error, AdminRoomMinibarRow[]>>
): UseQueryResult<AdminRoomMinibarRow[], Error> {
  return useQuery({
    queryKey: [...adminLodgingQueryKeys.room(id), 'minibar'] as const,
    queryFn: ({ signal }) => getAdminRoomMinibar(id, undefined, signal),
    enabled: id.length > 0,
    ...options,
  });
}

export function useAdminRoom(
  id: string,
  options?: Partial<UseQueryOptions<AdminRoomRow, Error, AdminRoomRow>>
): UseQueryResult<AdminRoomRow, Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.room(id),
    queryFn: ({ signal }) => getAdminRoom(id, undefined, signal),
    enabled: id.length > 0,
    ...options,
  });
}

export function useAdminFolios(
  query?: AdminFolioListQuery,
  options?: Partial<UseQueryOptions<AdminGuestFolioRow[], Error, AdminGuestFolioRow[]>>
): UseQueryResult<AdminGuestFolioRow[], Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.folios(query),
    queryFn: ({ signal }) => getAdminFolios(query, undefined, signal),
    ...options,
  });
}

export function useAdminFolio(
  id: string,
  options?: Partial<UseQueryOptions<AdminGuestFolioRow, Error, AdminGuestFolioRow>>
): UseQueryResult<AdminGuestFolioRow, Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.folio(id),
    queryFn: ({ signal }) => getAdminFolio(id, undefined, signal),
    enabled: id.length > 0,
    ...options,
  });
}

export function useAdminFolioItems(
  id: string,
  options?: Partial<UseQueryOptions<AdminFolioItem[], Error, AdminFolioItem[]>>
): UseQueryResult<AdminFolioItem[], Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.folioItems(id),
    queryFn: ({ signal }) => getAdminFolioItems(id, undefined, signal),
    enabled: id.length > 0,
    ...options,
  });
}

function useInvalidateLodging() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: adminLodgingQueryKeys.all });
  };
}

export function useCreateAdminRoom(): UseMutationResult<AdminRoomRow, Error, CreateAdminRoomRequest> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: (body: CreateAdminRoomRequest) => createAdminRoom(body),
    onSuccess: invalidate,
  });
}

export function useUpdateAdminRoomStatus(): UseMutationResult<
  AdminRoomRow,
  Error,
  { id: string; status: string }
> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: ({ id, status }) => updateAdminRoomStatus(id, status),
    onSuccess: invalidate,
  });
}

export function useUpdateAdminRoomDetails(): UseMutationResult<
  AdminRoomRow,
  Error,
  { id: string; body: UpdateAdminRoomDetailsRequest }
> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: ({ id, body }) => updateAdminRoomDetails(id, body),
    onSuccess: invalidate,
  });
}

export function useDeleteAdminRoom(): UseMutationResult<void, Error, string> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: (id: string) => deleteAdminRoom(id),
    onSuccess: invalidate,
  });
}

export function useChargeAdminFolio(): UseMutationResult<
  AdminGuestFolioRow,
  Error,
  { id: string; body: ChargeAdminFolioRequest }
> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: ({ id, body }) => chargeAdminFolio(id, body),
    onSuccess: invalidate,
  });
}

export function useMoveAdminFolio(): UseMutationResult<
  AdminGuestFolioRow,
  Error,
  { id: string; newRoomId: string }
> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: ({ id, newRoomId }) => moveAdminFolio(id, newRoomId),
    onSuccess: invalidate,
  });
}

export function useCheckoutAdminFolio(): UseMutationResult<AdminGuestFolioRow, Error, string> {
  const invalidate = useInvalidateLodging();
  return useMutation({
    mutationFn: (id: string) => checkoutAdminFolio(id),
    onSuccess: invalidate,
  });
}
