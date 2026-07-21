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
  expiresAt?: string | null;
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
  expiresAt?: string | null;
  tenantId?: string | null;
}

export const userEffectivePermissionsQueryKey = (userId: string) =>
  ['/api/UserManagement', userId, 'permissions', 'effective'] as const;

export async function getUserEffectivePermissions(
  userId: string
): Promise<UserEffectivePermissionsDto> {
  return customInstance<UserEffectivePermissionsDto>({
    url: `/api/UserManagement/${userId}/permissions/effective`,
    method: 'GET',
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
