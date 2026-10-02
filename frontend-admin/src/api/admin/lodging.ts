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

export const adminLodgingQueryKeys = {
  all: ['admin', 'lodging'] as const,
  rooms: () => [...adminLodgingQueryKeys.all, 'rooms'] as const,
  folios: () => [...adminLodgingQueryKeys.all, 'folios'] as const,
};

export function getAdminRooms(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminRoomRow[]> {
  return customInstance<AdminRoomRow[]>({ url: '/api/admin/rooms', method: 'GET', signal }, options).then(
    (res) => unwrapData<AdminRoomRow[]>(res)
  );
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

export function getAdminFolios(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AdminGuestFolioRow[]> {
  return customInstance<AdminGuestFolioRow[]>(
    { url: '/api/admin/folios', method: 'GET', signal },
    options
  ).then((res) => unwrapData<AdminGuestFolioRow[]>(res));
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

export function useAdminFolios(
  options?: Partial<UseQueryOptions<AdminGuestFolioRow[], Error, AdminGuestFolioRow[]>>
): UseQueryResult<AdminGuestFolioRow[], Error> {
  return useQuery({
    queryKey: adminLodgingQueryKeys.folios(),
    queryFn: ({ signal }) => getAdminFolios(undefined, signal),
    ...options,
  });
}

export function useCreateAdminRoom(): UseMutationResult<AdminRoomRow, Error, CreateAdminRoomRequest> {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateAdminRoomRequest) => createAdminRoom(body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminLodgingQueryKeys.rooms() });
    },
  });
}
