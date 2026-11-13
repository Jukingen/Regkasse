/**
 * Admin user management API — platform vs tenant separation.
 * @see GET /api/admin/users?type=platform|tenant
 * @see POST /api/admin/users (tenantId in body for mandant users)
 * @see DELETE /api/admin/tenants/{tenantId}/users/{userId}
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { AdminUserDto as GeneratedAdminUserDto, UserInfo } from '@/api/generated/model';
import {
  type CreateTenantUserRequest,
  type CreateTenantUserResult,
  createTenantUser,
} from '@/features/super-admin/api/tenantUsers';
import { customInstance } from '@/lib/axios';

/**
 * Admin user row from GET /api/admin/users.
 * Extends OpenAPI {@link GeneratedAdminUserDto} (`userName`, `tenantName`, …).
 * `twoFactorEnabled` is not yet in the OpenAPI schema.
 */
export type AdminUserDto = GeneratedAdminUserDto & {
  twoFactorEnabled?: boolean;
};

export type { GeneratedAdminUserDto };

export type TenantUserRowDto = {
  userId: string;
  userName: string;
  email: string;
  name: string;
  role: string;
  isOwner: boolean;
  isActive: boolean;
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  joinedAtUtc: string;
  lastLoginAt?: string | null;
  twoFactorEnabled?: boolean;
};

export type ListAdminUsersParams = {
  type?: 'platform' | 'tenant';
  tenantId?: string;
  role?: string;
  isActive?: boolean;
  search?: string;
};

/** Unified admin create (platform or tenant); password is generated server-side. */
export type CreateUserRequest = {
  email: string;
  firstName?: string;
  lastName?: string;
  role: string;
  isOwner?: boolean;
  tenantId?: string;
};

export type CreateUserResult = {
  userId: string;
  email: string;
  userName?: string;
  generatedPassword: string;
  forcePasswordChangeOnNextLogin: boolean;
  success: boolean;
  tenantPortalUrl?: string | null;
  tenantId?: string | null;
  tenantSlug?: string | null;
};

export type UsernameSuggestionResponse = {
  suggestedUsername: string;
  availableNumbers: number[];
};

export async function fetchUsernameSuggestion(role: string): Promise<UsernameSuggestionResponse> {
  return customInstance<UsernameSuggestionResponse>({
    url: '/api/admin/users/username-suggestions',
    method: 'GET',
    params: { role },
  });
}

type AdminCreateUserResponseDto = {
  id: string;
  email?: string | null;
  userName?: string | null;
  generatedPassword: string;
  forcePasswordChangeOnNextLogin?: boolean;
};

function mapTenantCreateResult(
  result: CreateTenantUserResult,
  requestedTenantId: string
): CreateUserResult {
  const responseTenantId = result.tenantId ?? requestedTenantId;
  if (result.tenantId && result.tenantId !== requestedTenantId) {
    throw new Error(
      `Tenant mismatch: requested ${requestedTenantId}, API returned ${result.tenantId}`
    );
  }
  return {
    userId: result.userId,
    email: result.email,
    userName: result.userName,
    generatedPassword: result.generatedPassword,
    forcePasswordChangeOnNextLogin: result.forcePasswordChangeOnNextLogin,
    success: result.success,
    tenantPortalUrl: result.tenantPortalUrl,
    tenantId: responseTenantId,
    tenantSlug: result.tenantSlug ?? undefined,
  };
}

export async function createUser(data: CreateUserRequest): Promise<CreateUserResult> {
  const email = data.email.trim();
  if (data.tenantId) {
    const tenantBody: CreateTenantUserRequest = {
      email,
      firstName: data.firstName,
      lastName: data.lastName,
      role: data.role,
      isOwner: data.isOwner,
    };
    const result = await createTenantUser(data.tenantId, tenantBody);
    return mapTenantCreateResult(result, data.tenantId);
  }
  return createPlatformUser({ ...data, email });
}

export async function createPlatformUser(
  data: Omit<CreateUserRequest, 'tenantId' | 'isOwner'>
): Promise<CreateUserResult> {
  const created = await customInstance<AdminCreateUserResponseDto>({
    url: '/api/admin/users',
    method: 'POST',
    data: {
      email: data.email.trim(),
      firstName: data.firstName,
      lastName: data.lastName,
      role: data.role,
    },
  });
  if (!created.id || !created.generatedPassword) {
    throw new Error('Create user response missing id or generatedPassword');
  }
  return {
    userId: created.id,
    email: created.email ?? data.email,
    userName: created.userName ?? undefined,
    generatedPassword: created.generatedPassword,
    forcePasswordChangeOnNextLogin: true,
    success: true,
  };
}

export type AdminUserTenantMembership = {
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
  role: string;
  isOwner: boolean;
};

export const adminUsersQueryKeys = {
  all: (isActive?: boolean, role?: string, search?: string) =>
    ['admin', 'users', 'all', isActive ?? 'all', role ?? 'all', search ?? ''] as const,
  detail: (userId: string) => ['admin', 'users', userId, 'detail'] as const,
  userTenants: (userId: string) => ['admin', 'users', userId, 'tenants'] as const,
  platform: (isActive?: boolean, search?: string) =>
    ['admin', 'users', 'platform', isActive ?? 'all', search ?? ''] as const,
  tenant: (tenantId?: string, role?: string, search?: string) =>
    ['admin', 'users', 'tenant', tenantId ?? 'all', role ?? 'all', search ?? ''] as const,
};

export async function getAdminUserById(userId: string): Promise<AdminUserDto> {
  return customInstance<AdminUserDto>({
    url: `/api/admin/users/${userId}`,
    method: 'GET',
  });
}

export async function getAdminUserTenants(userId: string): Promise<AdminUserTenantMembership[]> {
  const rows = await customInstance<
    Array<{
      tenantId: string;
      tenantName: string;
      tenantSlug: string;
      role: string;
      isOwner: boolean;
    }>
  >({
    url: `/api/admin/users/${userId}/tenants`,
    method: 'GET',
  });
  return rows.map((r) => ({
    tenantId: String(r.tenantId),
    tenantName: r.tenantName,
    tenantSlug: r.tenantSlug,
    role: r.role,
    isOwner: r.isOwner,
  }));
}

export async function updateUserTenants(userId: string, tenantIds: string[]): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/users/${userId}/tenants`,
    method: 'PUT',
    data: { tenantIds },
  });
}

/** Unified user list (platform + tenant) with tenant metadata per row. */
function listSearchParams(params?: Pick<ListAdminUsersParams, 'isActive' | 'role' | 'search'>) {
  const search = params?.search?.trim();
  return {
    ...(params?.isActive != null ? { isActive: params.isActive } : {}),
    ...(params?.role ? { role: params.role } : {}),
    ...(search ? { search } : {}),
  };
}

export async function listAllAdminUsers(
  params?: Pick<ListAdminUsersParams, 'isActive' | 'role' | 'search'>
): Promise<AdminUserDto[]> {
  return customInstance<AdminUserDto[]>({
    url: '/api/admin/users',
    method: 'GET',
    params: listSearchParams(params),
  });
}

export async function listPlatformUsers(
  params?: Pick<ListAdminUsersParams, 'isActive' | 'search'>
): Promise<AdminUserDto[]> {
  return customInstance<AdminUserDto[]>({
    url: '/api/admin/users',
    method: 'GET',
    params: { type: 'platform', ...listSearchParams(params) },
  });
}

export async function listTenantUsers(params?: ListAdminUsersParams): Promise<TenantUserRowDto[]> {
  return customInstance<TenantUserRowDto[]>({
    url: '/api/admin/users',
    method: 'GET',
    params: {
      type: 'tenant',
      ...(params?.tenantId ? { tenantId: params.tenantId } : {}),
      ...listSearchParams(params),
    },
  });
}

/** Removes membership only — does not delete the user account. */
export async function removeUserFromTenant(tenantId: string, userId: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/tenants/${tenantId}/users/${userId}`,
    method: 'DELETE',
  });
}

export type UpdateUserRoleRequest = {
  role: string;
};

export type UpdateUserRoleResult = {
  userId: string;
  userName: string;
  email: string;
  name: string;
  role: string;
  isOwner: boolean;
  joinedAtUtc: string;
};

/** Assign a new role to a tenant user. */
export async function updateUserRole(
  tenantId: string,
  userId: string,
  body: UpdateUserRoleRequest
): Promise<UpdateUserRoleResult> {
  return customInstance<UpdateUserRoleResult>({
    url: `/api/admin/tenants/${tenantId}/users/${userId}/role`,
    method: 'PUT',
    data: {
      role: body.role,
    },
  });
}

export function adminUserToUserInfo(dto: AdminUserDto): UserInfo {
  return {
    id: dto.id ?? undefined,
    userName: dto.userName ?? undefined,
    email: dto.email ?? undefined,
    firstName: dto.firstName ?? '',
    lastName: dto.lastName ?? '',
    employeeNumber: dto.employeeNumber ?? undefined,
    role: dto.role ?? undefined,
    taxNumber: dto.taxNumber ?? undefined,
    notes: dto.notes ?? undefined,
    isActive: dto.isActive ?? false,
    createdAt: dto.createdAt,
    lastLoginAt: dto.lastLoginAt ?? undefined,
  };
}

export function tenantRowToTenantUser(row: TenantUserRowDto) {
  return {
    userId: row.userId,
    userName: row.userName,
    email: row.email,
    name: row.name,
    role: row.role,
    isOwner: row.isOwner,
    joinedAtUtc: row.joinedAtUtc,
    tenantId: row.tenantId,
    tenantSlug: row.tenantSlug,
    tenantName: row.tenantName,
    isActive: row.isActive,
    lastLoginAt: row.lastLoginAt ?? undefined,
    twoFactorEnabled: row.twoFactorEnabled,
  };
}

export type TenantUserRow = ReturnType<typeof tenantRowToTenantUser>;

export type UpdateAdminUsernameRequest = {
  newUsername: string;
  reason?: string | null;
};

export type UpdateAdminUsernameResponse = {
  oldUsername?: string | null;
  newUsername: string;
};

export async function updateAdminUsername(
  userId: string,
  body: UpdateAdminUsernameRequest
): Promise<UpdateAdminUsernameResponse> {
  return customInstance<UpdateAdminUsernameResponse>({
    url: `/api/admin/users/${userId}/username`,
    method: 'PATCH',
    data: {
      newUsername: body.newUsername.trim(),
      reason: body.reason?.trim() || undefined,
    },
  });
}

export function useUpdateAdminUsernameMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vars: UpdateAdminUsernameRequest & { userId: string }) =>
      updateAdminUsername(vars.userId, {
        newUsername: vars.newUsername,
        reason: vars.reason,
      }),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      void queryClient.invalidateQueries({
        queryKey: adminUsersQueryKeys.detail(variables.userId),
      });
      void queryClient.invalidateQueries({
        queryKey: adminUsersQueryKeys.userTenants(variables.userId),
      });
    },
  });
}
