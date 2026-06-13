'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import { useCallback, useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useI18n } from '@/i18n/I18nProvider';
import { useUsersPolicy } from '@/shared/auth/usersPolicy';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { usePermissionsCatalog } from '@/features/users/hooks/usePermissionsCatalog';
import {
    createRole as gatewayCreateRole,
    deleteRole as gatewayDeleteRole,
    normalizeError,
    rolesQueryKey,
    rolesWithPermissionsQueryKey,
    updateRolePermissions as gatewayUpdateRolePermissions,
} from '@/features/users/api/usersGateway';
import { usersCopy } from '@/features/users/constants/copy';

export function useRoleManagementWorkspace() {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const policy = useUsersPolicy();
    const [createRoleOpen, setCreateRoleOpen] = useState(false);

    const enabled = policy.canCreateRole || policy.canDeleteRole || policy.canEditRolePermissions;

    const rolesQuery = useRolesWithPermissions({ enabled });
    const catalogQuery = usePermissionsCatalog({ enabled });

    const createRoleMutation = useMutation({
        mutationFn: (data: { name: string }) => gatewayCreateRole({ name: data.name.trim() }),
        onSuccess: () => {
            message.success(t('users.messages.roleCreated'));
            void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
            void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            setCreateRoleOpen(false);
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorGeneric).message);
        },
    });

    const updateRolePermissionsMutation = useMutation({
        mutationFn: ({ roleName, permissions }: { roleName: string; permissions: string[] }) =>
            gatewayUpdateRolePermissions(roleName, permissions),
        onSuccess: () => {
            message.success(usersCopy.successPermissionsSaved);
            void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorSavePermissions).message);
        },
    });

    const deleteRoleMutation = useMutation({
        mutationFn: (roleName: string) => gatewayDeleteRole(roleName),
        onSuccess: () => {
            message.success(usersCopy.successRoleDeleted);
            void queryClient.invalidateQueries({ queryKey: rolesWithPermissionsQueryKey });
            void queryClient.invalidateQueries({ queryKey: rolesQueryKey });
        },
        onError: (e: unknown) => {
            message.error(normalizeError(e, usersCopy.errorDeleteRole).message);
        },
    });

    const handleRetry = useCallback(() => {
        void rolesQuery.refetch();
        void catalogQuery.refetch();
    }, [rolesQuery, catalogQuery]);

    const handleCreateRoleConfirm = useCallback(
        (name: string) => {
            if (!policy.canCreateRole) {
                message.error(usersCopy.noPermission);
                return;
            }
            createRoleMutation.mutate({ name });
        },
        [createRoleMutation, message, policy.canCreateRole],
    );

    const handleSaveRolePermissions = useCallback(
        async (roleName: string, permissions: string[]) => {
            await updateRolePermissionsMutation.mutateAsync({ roleName, permissions });
        },
        [updateRolePermissionsMutation],
    );

    const handleDeleteRole = useCallback(
        async (roleName: string) => {
            await deleteRoleMutation.mutateAsync(roleName);
        },
        [deleteRoleMutation],
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
            handleSaveRolePermissions,
            handleDeleteRole,
            updateRolePermissionsMutation.isPending,
            deleteRoleMutation.isPending,
            createRoleMutation.isPending,
        ],
    );
}
