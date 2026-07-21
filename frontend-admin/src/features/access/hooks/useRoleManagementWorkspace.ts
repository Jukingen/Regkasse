'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';

import {
  deleteRole as gatewayDeleteRole,
  updateRolePermissions as gatewayUpdateRolePermissions,
  normalizeError,
  rolesQueryKey,
  rolesWithPermissionsQueryKey,
} from '@/features/users/api/usersGateway';
import { useCreateRoleMutation } from '@/features/users/hooks/useCreateRoleMutation';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n/I18nProvider';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';

export function useRoleManagementWorkspace() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const policy = useUsersPolicy();
  const [createRoleOpen, setCreateRoleOpen] = useState(false);

  const enabled = policy.canCreateRole || policy.canDeleteRole || policy.canEditRolePermissions;

  const rolesQuery = useRolesWithPermissions({ enabled });
  const catalogQuery = usePermissionsCatalog({ enabled });

  const createRoleMutation = useCreateRoleMutation({
    onSuccess: () => setCreateRoleOpen(false),
  });

  const updateRolePermissionsMutation = useMutation({
    mutationFn: ({ roleName, permissions }: { roleName: string; permissions: string[] }) =>
      gatewayUpdateRolePermissions(roleName, permissions),
    onSuccess: () => {
      message.success(t('users.roleDrawer.successPermissionsSaved'));
      void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
      void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
    },
    onError: (e: unknown) => {
      message.error(normalizeError(e, t('users.roleDrawer.errorSavePermissions')).message);
    },
  });

  const deleteRoleMutation = useMutation({
    mutationFn: (roleName: string) => gatewayDeleteRole(roleName),
    onSuccess: () => {
      message.success(t('users.roleDrawer.successRoleDeleted'));
      void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
      void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
    },
    onError: (e: unknown) => {
      message.error(normalizeError(e, t('users.roleDrawer.errorDeleteRole')).message);
    },
  });

  const handleRetry = useCallback(() => {
    void rolesQuery.refetch();
    void catalogQuery.refetch();
  }, [rolesQuery, catalogQuery]);

  const inheritRoleOptions = useMemo(
    () =>
      (rolesQuery.data ?? []).map((role) => ({
        value: role.roleName,
        label: formatRoleDisplayLabel(t, role.roleName),
      })),
    [rolesQuery.data, t]
  );

  const handleCreateRoleConfirm = useCallback(
    (payload: { name: string; inheritFromRole?: string }) => {
      if (!policy.canCreateRole) {
        message.error(t('users.roleDrawer.noPermission'));
        return;
      }
      createRoleMutation.mutate(payload);
    },
    [createRoleMutation, message, policy.canCreateRole, t]
  );

  const handleSaveRolePermissions = useCallback(
    async (roleName: string, permissions: string[]) => {
      await updateRolePermissionsMutation.mutateAsync({ roleName, permissions });
    },
    [updateRolePermissionsMutation]
  );

  const handleDeleteRole = useCallback(
    async (roleName: string) => {
      await deleteRoleMutation.mutateAsync(roleName);
    },
    [deleteRoleMutation]
  );

  return useMemo(
    () => ({
      policy,
      enabled,
      roles: rolesQuery.data,
      catalog: catalogQuery.data,
      rolesLoading: rolesQuery.isLoading,
      catalogLoading: catalogQuery.isLoading,
      rolesError: rolesQuery.isError,
      catalogError: catalogQuery.isError,
      onRetry: handleRetry,
      createRoleOpen,
      setCreateRoleOpen,
      handleCreateRoleConfirm,
      inheritRoleOptions,
      handleSaveRolePermissions,
      handleDeleteRole,
      saveLoading: updateRolePermissionsMutation.isPending,
      deleteLoading: deleteRoleMutation.isPending,
      createRoleLoading: createRoleMutation.isPending,
    }),
    [
      policy,
      enabled,
      rolesQuery.data,
      rolesQuery.isLoading,
      rolesQuery.isError,
      catalogQuery.data,
      catalogQuery.isLoading,
      catalogQuery.isError,
      handleRetry,
      createRoleOpen,
      handleCreateRoleConfirm,
      inheritRoleOptions,
      handleSaveRolePermissions,
      handleDeleteRole,
      updateRolePermissionsMutation.isPending,
      deleteRoleMutation.isPending,
      createRoleMutation.isPending,
    ]
  );
}
