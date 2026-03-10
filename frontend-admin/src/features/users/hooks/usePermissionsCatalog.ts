/**
 * Permissions catalog – for role management permission checklist (grouped).
 */
import { useQuery } from '@tanstack/react-query';
import { getPermissionsCatalog, permissionsCatalogQueryKey } from '../api/usersGateway';

export function usePermissionsCatalog(options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: permissionsCatalogQueryKey,
    queryFn: getPermissionsCatalog,
    enabled: options?.enabled !== false,
  });
}
