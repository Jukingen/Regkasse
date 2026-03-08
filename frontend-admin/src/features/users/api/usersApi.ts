/**
 * User Management API – RKSV/DSGVO uyumlu liste filtreleri ve deactivate/reactivate.
 * Liste: tek endpoint, server-side pagination + birleşik filtre (query + role + isActive).
 */
import { customInstance } from '@/lib/axios';
import type { UserInfo } from '@/api/generated/model';

export type UsersListParams = {
  role?: string;
  isActive?: boolean;
  query?: string;
  page?: number;
  pageSize?: number;
};

export type UsersListPagination = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type UsersListResponse = {
  items: UserInfo[];
  pagination: UsersListPagination;
};

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;

export async function getUsersList(params?: UsersListParams): Promise<UsersListResponse> {
  const page = params?.page ?? DEFAULT_PAGE;
  const pageSize = params?.pageSize ?? DEFAULT_PAGE_SIZE;
  const searchParams: Record<string, string | number | boolean | undefined> = {
    page,
    pageSize,
  };
  if (params?.role != null) searchParams.role = params.role;
  if (params?.isActive != null) searchParams.isActive = params.isActive;
  const query = (params?.query ?? '').trim();
  if (query) searchParams.query = query;

  return customInstance<UsersListResponse>({
    url: '/api/UserManagement',
    method: 'GET',
    params: searchParams,
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
