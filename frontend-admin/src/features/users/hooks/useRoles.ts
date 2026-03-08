/**
 * Roller listesi – gateway üzerinden; invalidation rolesQueryKey ile.
 */
import { useQuery } from '@tanstack/react-query';
import { getRoles, rolesQueryKey } from '../api/usersGateway';

export function useRoles(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: rolesQueryKey,
    queryFn: getRoles,
    enabled: options?.enabled !== false,
  });
}
