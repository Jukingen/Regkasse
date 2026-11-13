'use client';

import { useQuery } from '@tanstack/react-query';

import { adminUsersQueryKeys, getAdminUserTenants } from '@/features/users/api/users';

export function useAdminUserTenants(userId: string | null | undefined, enabled = true) {
  return useQuery({
    queryKey: adminUsersQueryKeys.userTenants(userId ?? ''),
    queryFn: () => getAdminUserTenants(userId!),
    enabled: enabled && !!userId,
  });
}
