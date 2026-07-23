'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  createRole as gatewayCreateRole,
  getPermissionsCatalog,
  rolesQueryKey,
  rolesWithPermissionsQueryKey,
  updateRolePermissions,
} from '@/features/users/api/usersGateway';
import {
  findRolePresetById,
  getPresetKeysInCatalog,
} from '@/features/users/constants/rolePresets';
import { getCreateRoleErrorMessage } from '@/features/users/utils/createRoleErrors';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';

export type CreateRolePayload = {
  name: string;
  inheritFromRole?: string;
  /** Optional role preset id — applied after create when no inheritFromRole. */
  presetId?: string;
};

export type UseCreateRoleMutationOptions = {
  onSuccess?: () => void;
};

export function useCreateRoleMutation(options: UseCreateRoleMutationOptions = {}) {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: CreateRolePayload) => {
      const name = data.name.trim();
      const inheritFromRole = data.inheritFromRole?.trim() || undefined;
      const presetId = data.presetId?.trim() || undefined;

      await gatewayCreateRole({
        name,
        inheritFromRole,
      });

      // Preset and inherit are mutually exclusive in the create UI; prefer inherit if both set.
      if (!inheritFromRole && presetId) {
        const preset = findRolePresetById(presetId);
        if (preset) {
          const catalog = await getPermissionsCatalog();
          const keys = getPresetKeysInCatalog(
            preset,
            catalog.map((item) => item.key)
          );
          if (keys.length > 0) {
            await updateRolePermissions(name, keys);
          }
        }
      }

      return { name, inheritFromRole, presetId };
    },
    onSuccess: (_data, variables) => {
      const inheritFromRole = variables.inheritFromRole?.trim();
      const preset = findRolePresetById(variables.presetId);
      if (inheritFromRole) {
        message.success(
          t('users.createRole.successWithInherit', {
            role: variables.name.trim(),
            source: formatRoleDisplayLabel(t, inheritFromRole),
          })
        );
      } else if (preset) {
        message.success(
          t('users.createRole.successWithPreset', {
            role: variables.name.trim(),
            preset: preset.label,
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
