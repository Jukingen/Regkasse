'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  createRole as gatewayCreateRole,
  rolesQueryKey,
  rolesWithPermissionsQueryKey,
} from '@/features/users/api/usersGateway';
import { getCreateRoleErrorMessage } from '@/features/users/utils/createRoleErrors';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';

export type CreateRolePayload = {
  name: string;
  inheritFromRole?: string;
};

export type UseCreateRoleMutationOptions = {
  onSuccess?: () => void;
};

export function useCreateRoleMutation(options: UseCreateRoleMutationOptions = {}) {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateRolePayload) =>
      gatewayCreateRole({
        name: data.name.trim(),
        inheritFromRole: data.inheritFromRole?.trim() || undefined,
      }),
    onSuccess: (_data, variables) => {
      const inheritFromRole = variables.inheritFromRole?.trim();
      if (inheritFromRole) {
        message.success(
          t('users.createRole.successWithInherit', {
            role: variables.name.trim(),
            source: formatRoleDisplayLabel(t, inheritFromRole),
          })
        );
      } else {
        message.success(t('users.messages.roleCreated'));
      }
      void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
      void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
      options.onSuccess?.();
    },
    onError: (error: unknown) => {
      message.error(getCreateRoleErrorMessage(t, error));
    },
  });
}
