import { customInstance } from '@/lib/axios';

export interface UserPermissionOverrideDto {
  id: string;
  userId: string;
  tenantId?: string | null;
  permission: string;
  isGranted: boolean;
  reason?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
  validFrom?: string | null;
  expiresAt?: string | null;
  /** scheduled | active | expiringSoon | expired */
  status?: string | null;
}

export interface UserEffectivePermissionsDto {
  rolePermissions: string[];
  overrides: UserPermissionOverrideDto[];
  effectivePermissions: string[];
}

export interface UpsertUserPermissionOverrideRequest {
  permission: string;
  isGranted: boolean;
  reason?: string | null;
  validFrom?: string | null;
  expiresAt?: string | null;
  tenantId?: string | null;
}

export const userEffectivePermissionsQueryKey = (userId: string) =>
  ['/api/UserManagement', userId, 'permissions', 'effective'] as const;

export const userPermissionOverridesQueryKey = (userId: string, includeExpired = false) =>
  ['/api/UserManagement', userId, 'permissions', 'overrides', { includeExpired }] as const;

export async function getUserEffectivePermissions(
  userId: string
): Promise<UserEffectivePermissionsDto> {
  return customInstance<UserEffectivePermissionsDto>({
    url: `/api/UserManagement/${userId}/permissions/effective`,
    method: 'GET',
  });
}

export async function listUserPermissionOverrides(
  userId: string,
  options?: { includeExpired?: boolean; tenantId?: string | null }
): Promise<UserPermissionOverrideDto[]> {
  return customInstance<UserPermissionOverrideDto[]>({
    url: `/api/UserManagement/${userId}/permissions/overrides`,
    method: 'GET',
    params: {
      includeExpired: options?.includeExpired ? 'true' : undefined,
      tenantId: options?.tenantId ?? undefined,
    },
  });
}

export async function upsertUserPermissionOverride(
  userId: string,
  body: UpsertUserPermissionOverrideRequest
): Promise<UserPermissionOverrideDto> {
  return customInstance<UserPermissionOverrideDto>({
    url: `/api/UserManagement/${userId}/permissions/overrides`,
    method: 'PUT',
    data: body,
  });
}

export async function deleteUserPermissionOverride(
  userId: string,
  overrideId: string
): Promise<void> {
  await customInstance<void>({
    url: `/api/UserManagement/${userId}/permissions/overrides/${overrideId}`,
    method: 'DELETE',
  });
}
