/**
 * Roles with permissions list – for role management drawer.
 */
import { useQuery } from '@tanstack/react-query';
import { getRolesWithPermissions, rolesWithPermissionsQueryKey } from '../api/usersGateway';

export function useRolesWithPermissions(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: rolesWithPermissionsQueryKey,
    queryFn: getRolesWithPermissions,
    enabled: options?.enabled !== false,
  });
}
