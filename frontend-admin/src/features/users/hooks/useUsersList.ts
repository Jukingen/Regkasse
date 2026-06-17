/**
 * Kullanıcı listesi – gateway üzerinden; server-side pagination + birleşik filtre.
 */
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getUsersList, listQueryKey, type UsersListParams } from '../api/usersGateway';

export const usersListQueryKey = listQueryKey;

export function useUsersList(
  params?: UsersListParams,
  options?: { enabled?: boolean; staleTime?: number },
) {
  return useQuery({
    queryKey: [...listQueryKey, params],
    queryFn: () => getUsersList(params),
    enabled: options?.enabled !== false,
    staleTime: options?.staleTime,
    placeholderData: keepPreviousData,
  });
}
