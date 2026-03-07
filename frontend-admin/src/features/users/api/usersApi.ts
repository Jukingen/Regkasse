/**
 * User Management API – RKSV/DSGVO uyumlu liste filtreleri ve deactivate/reactivate.
 * Generated client'da olmayan endpoint'ler (backend'de eklendi) buradan çağrılır.
 */
import { customInstance } from '@/lib/axios';
import type { UserInfo } from '@/api/generated/model';

export type UsersListParams = {
  role?: string;
  isActive?: boolean;
  query?: string;
};

export async function getUsersList(params?: UsersListParams): Promise<UserInfo[]> {
  const search = (params?.query ?? '').trim();
  if (search) {
    return customInstance<UserInfo[]>({
      url: '/api/UserManagement/search',
      method: 'GET',
      params: { query: search },
    });
  }
  const searchParams = new URLSearchParams();
  if (params?.role != null) searchParams.set('role', params.role);
  if (params?.isActive != null) searchParams.set('isActive', String(params.isActive));
  const qs = searchParams.toString();
  return customInstance<UserInfo[]>({
    url: `/api/UserManagement${qs ? `?${qs}` : ''}`,
    method: 'GET',
  });
}

export type DeactivateUserRequest = { reason: string };
export async function deactivateUser(id: string, body: DeactivateUserRequest): Promise<void> {
  return customInstance<void>({
    url: `/api/UserManagement/${id}/deactivate`,
    method: 'PUT',
    data: body,
  });
}

export type ReactivateUserRequest = { reason?: string };
export async function reactivateUser(id: string, body?: ReactivateUserRequest): Promise<void> {
  return customInstance<void>({
    url: `/api/UserManagement/${id}/reactivate`,
    method: 'PUT',
    data: body ?? {},
  });
}
