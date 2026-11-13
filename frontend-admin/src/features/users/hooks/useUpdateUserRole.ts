'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  type UpdateUserRoleRequest,
  adminUsersQueryKeys,
  updateUserRole,
} from '@/features/users/api/users';

export type UpdateUserRoleVariables = {
  tenantId: string;
  userId: string;
} & UpdateUserRoleRequest;

export type UseUpdateUserRoleOptions = {
  onSuccess?: () => void;
  onError?: () => void;
};

export function useUpdateUserRole(options: UseUpdateUserRoleOptions = {}) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ tenantId, userId, role }: UpdateUserRoleVariables) =>
      updateUserRole(tenantId, userId, { role }),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
      void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.all() });
      void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
      void queryClient.invalidateQueries({
        queryKey: ['admin', 'tenant-users', variables.tenantId],
      });
      void queryClient.invalidateQueries({
        queryKey: ['/api/UserManagement', variables.userId, 'permissions', 'effective'],
      });
      options.onSuccess?.();
    },
    onError: () => {
      options.onError?.();
    },
  });
}
