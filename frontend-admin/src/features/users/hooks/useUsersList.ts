/**
 * Kullanıcı listesi – role, status, search (RKSV uyumlu backend GET/search parametreleri).
 */
import { useQuery } from '@tanstack/react-query';
import { getUsersList, type UsersListParams } from '../api/usersApi';

export const usersListQueryKey = ['/api/UserManagement'] as const;

export function useUsersList(params?: UsersListParams) {
  return useQuery({
    queryKey: [...usersListQueryKey, params],
    queryFn: () => getUsersList(params),
  });
}
