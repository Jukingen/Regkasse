/**
 * Users API Gateway – Tek giriş noktası.
 * Generated + custom çağrıları normalize eder; query key ve hata adaptörü tek yerde.
 */
import {
  postApiUserManagement,
  putApiUserManagementId,
  putApiUserManagementIdResetPassword,
  getApiUserManagementRoles,
  postApiUserManagementRoles,
} from '@/api/generated/user-management/user-management';
import {
  getUsersList as getUsersListFromApi,
  deactivateUser as deactivateUserFromApi,
  reactivateUser as reactivateUserFromApi,
} from './usersApi';
import type { CreateUserRequest, UpdateUserRequest, ResetPasswordRequest, UserInfo } from '@/api/generated/model';
import type { CreateRoleRequest } from '@/api/generated/model';
import type { UsersListParams, DeactivateUserRequest, ReactivateUserRequest } from './usersApi';

// --- Query keys (invalidation tek yerden) ---
export const listQueryKey = ['/api/UserManagement'] as const;
export const rolesQueryKey = ['/api/UserManagement/roles'] as const;

// --- Liste (server-side pagination + birleşik filtre) ---
export { getUsersListFromApi as getUsersList };
export type { UsersListParams, UsersListResponse, UsersListPagination } from './usersApi';

// --- Roller ---
export async function getRoles(): Promise<string[]> {
  return getApiUserManagementRoles();
}

// --- Mutations (generated + custom) ---
export async function createUser(data: CreateUserRequest): Promise<UserInfo> {
  return postApiUserManagement(data);
}

export async function updateUser(id: string, data: UpdateUserRequest): Promise<void> {
  return putApiUserManagementId(id, data);
}

export async function resetPassword(id: string, data: ResetPasswordRequest): Promise<void> {
  return putApiUserManagementIdResetPassword(id, data);
}

export async function createRole(data: CreateRoleRequest): Promise<void> {
  return postApiUserManagementRoles(data);
}

export {
  deactivateUserFromApi as deactivateUser,
  reactivateUserFromApi as reactivateUser,
};
export type { DeactivateUserRequest, ReactivateUserRequest } from './usersApi';

// --- Hata adaptörü (backend { message } / { code, reason } tek forma) ---
export interface NormalizedError {
  message: string;
  code?: string;
  details?: unknown;
}

const FALLBACK_MESSAGE = 'Fehler.';

function firstValidationMessage(errors: unknown): string | undefined {
  if (errors == null || typeof errors !== 'object') return undefined;
  const obj = errors as Record<string, unknown>;
  for (const key of Object.keys(obj)) {
    const val = obj[key];
    if (Array.isArray(val) && val.length > 0 && typeof val[0] === 'string') return val[0];
    if (typeof val === 'string') return val;
  }
  return undefined;
}

export function normalizeError(error: unknown, fallbackMessage: string = FALLBACK_MESSAGE): NormalizedError {
  if (error == null) return { message: fallbackMessage };
  const err = error as { response?: { data?: { message?: string; reason?: string; code?: string; errors?: unknown; [k: string]: unknown } }; message?: string };
  const data = err.response?.data;
  const validationMsg = data?.errors != null ? firstValidationMessage(data.errors) : undefined;
  const message =
    (typeof data?.message === 'string' && data.message) ||
    validationMsg ||
    (typeof data?.reason === 'string' && data.reason) ||
    (typeof err.message === 'string' && err.message) ||
    fallbackMessage;
  const code = typeof data?.code === 'string' ? data.code : undefined;
  return { message, code, details: data };
}

// Re-export tipler (sayfa sadece gateway'den import etsin)
export type { UserInfo, CreateUserRequest, UpdateUserRequest } from '@/api/generated/model';
