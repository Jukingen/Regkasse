/**
 * Users API Gateway – Tek giriş noktası.
 * Generated + custom çağrıları normalize eder; query key ve hata adaptörü tek yerde.
 */
import {
  postApiUserManagement,
  putApiUserManagementId,
  putApiUserManagementIdResetPassword,
  getApiUserManagementId,
  getApiUserManagementRoles,
  postApiUserManagementRoles,
} from '@/api/generated/user-management/user-management';
import { authStorage } from '@/features/auth/services/authStorage';
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

// --- Tekil kullanıcı (edit drawer için tam DTO, Notes dahil) ---
/** Pick first defined from possible backend key names (camelCase, PascalCase, snake_case, etc.). */
function pick<T>(obj: Record<string, unknown>, ...keys: string[]): T | undefined {
  for (const k of keys) {
    if (Object.prototype.hasOwnProperty.call(obj, k) && obj[k] !== undefined) return obj[k] as T;
  }
  return undefined;
}

/**
 * Normalize GET /api/UserManagement/{id} response to UserInfo (camelCase).
 * Handles: direct body (customInstance returns response.data), PascalCase or camelCase, alternate names.
 */
function normalizeUserInfo(raw: Record<string, unknown>): UserInfo {
  return {
    id: pick<string>(raw, 'id', 'Id') ?? undefined,
    userName: pick<string>(raw, 'userName', 'UserName') ?? undefined,
    email: pick<string>(raw, 'email', 'Email') ?? undefined,
    firstName: pick<string>(raw, 'firstName', 'FirstName') ?? '',
    lastName: pick<string>(raw, 'lastName', 'LastName') ?? '',
    employeeNumber: pick<string>(raw, 'employeeNumber', 'EmployeeNumber', 'employeeNo', 'employee_no') ?? undefined,
    role: pick<string>(raw, 'role', 'Role', 'roleId', 'roleName') ?? undefined,
    taxNumber: pick<string>(raw, 'taxNumber', 'TaxNumber', 'taxNo', 'tax_no', 'vatNumber') ?? undefined,
    notes: pick<string>(raw, 'notes', 'Notes', 'comment', 'description') ?? undefined,
    isActive: pick<boolean>(raw, 'isActive', 'IsActive') ?? true,
    createdAt: pick<string>(raw, 'createdAt', 'CreatedAt') ?? undefined,
    lastLoginAt: pick<string>(raw, 'lastLoginAt', 'LastLoginAt') ?? undefined,
  };
}

export async function getUserById(id: string): Promise<UserInfo> {
  let raw: unknown = await getApiUserManagementId(id);
  // If backend ever wraps in { data: ... } or { result: ... }, unwrap once
  if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
    const obj = raw as Record<string, unknown>;
    if (obj.data && typeof obj.data === 'object') raw = obj.data;
    else if (obj.result && typeof obj.result === 'object') raw = obj.result;
  }
  const normalized = normalizeUserInfo((raw ?? {}) as Record<string, unknown>);
  return { ...normalized } as UserInfo;
}

export const getUserByIdQueryKey = (id: string) => ['/api/UserManagement', id] as const;

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

/** Reset password (admin): ensures Bearer token is sent; fails fast if not authenticated. */
export async function resetPassword(id: string, data: ResetPasswordRequest): Promise<void> {
  const token = typeof window !== 'undefined' ? authStorage.getToken() : null;
  if (!token) {
    return Promise.reject(
      Object.assign(new Error('Nicht angemeldet. Bitte erneut anmelden.'), { response: { status: 401, data: { message: 'Nicht angemeldet. Bitte erneut anmelden.' } } })
    );
  }
  return putApiUserManagementIdResetPassword(id, data, {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
  });
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
