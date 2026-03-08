/**
 * Kullanıcı listesi – role, status, search (RKSV uyumlu backend GET/search parametreleri).
 */
import { useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { getUsersList, type UsersListParams } from '../api/usersApi';

export const usersListQueryKey = ['/api/UserManagement'] as const;

export function useUsersList(
  params?: UsersListParams,
  options?: { enabled?: boolean }
) {
  return useQuery({
    queryKey: [...usersListQueryKey, params],
    queryFn: () => getUsersList(params),
    enabled: options?.enabled !== false,
  });
}
