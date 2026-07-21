'use client';

import { useMemo } from 'react';

import type { UsersListParams } from '@/features/users/api/usersGateway';
import { useUsersList } from '@/features/users/hooks/useUsersList';
import { isPlatformUserRole } from '@/features/users/utils/userScope';

/**
 * Tenant-scoped staff list — excludes platform operator rows (SuperAdmin).
 * Backed by GET /api/UserManagement (EF tenant filter on backend).
 */
export function useTenantStaff(params?: UsersListParams, options?: { enabled?: boolean }) {
  const listQuery = useUsersList(params, options);

  const staff = useMemo(
    () => (listQuery.data?.items ?? []).filter((user) => !isPlatformUserRole(user.role)),
    [listQuery.data?.items]
  );

  return {
    staff,
    pagination: listQuery.data?.pagination,
    isLoading: listQuery.isLoading,
    isFetching: listQuery.isFetching,
    isError: listQuery.isError,
    error: listQuery.error,
    refetch: listQuery.refetch,
    isPlaceholderData: listQuery.isPlaceholderData,
  };
}
