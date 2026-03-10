/**
 * Role management API – permissions catalog, roles with permissions, update permissions, delete role.
 * Backend contract: GET permissions-catalog, GET with-permissions, PUT roles/{roleName}/permissions, DELETE roles/{roleName}.
 */
import { customInstance } from '@/lib/axios';

export interface PermissionCatalogItemDto {
  key: string;
  group: string;
  resource: string;
  action: string;
  description?: string | null;
}

export interface RoleWithPermissionsDto {
  roleName: string;
  permissions: string[];
  isSystemRole: boolean;
  userCount: number;
  /** True only for custom roles with no assigned users. */
  canDelete?: boolean;
  /** True only for custom roles; system roles have fixed permissions. */
  canEditPermissions?: boolean;
}

export async function getPermissionsCatalog(): Promise<PermissionCatalogItemDto[]> {
  const data = await customInstance<PermissionCatalogItemDto[]>({
    url: '/api/UserManagement/roles/permissions-catalog',
    method: 'GET',
  });
  return Array.isArray(data) ? data : [];
}

export async function getRolesWithPermissions(): Promise<RoleWithPermissionsDto[]> {
  const data = await customInstance<RoleWithPermissionsDto[]>({
    url: '/api/UserManagement/roles/with-permissions',
    method: 'GET',
  });
  return Array.isArray(data) ? data : [];
}

export async function updateRolePermissions(roleName: string, permissions: string[]): Promise<void> {
  await customInstance<void>({
    url: `/api/UserManagement/roles/${encodeURIComponent(roleName)}/permissions`,
    method: 'PUT',
    data: { permissions },
  });
}

export async function deleteRole(roleName: string): Promise<void> {
  await customInstance<void>({
    url: `/api/UserManagement/roles/${encodeURIComponent(roleName)}`,
    method: 'DELETE',
  });
}
