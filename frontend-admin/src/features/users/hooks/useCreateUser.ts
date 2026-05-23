'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';

import type { CreateUserFormValues } from '@/features/users/components/CreateUserModal';
import {
    adminUsersQueryKeys,
    createUser,
    type CreateUserRequest,
} from '@/features/users/api/users';
import { useI18n } from '@/i18n';

export type UseCreateUserOptions = {
    /** When set, always creates under this mandant (tenant detail). */
    fixedTenantId?: string;
    onSuccess?: () => void;
    onError?: () => void;
};

function toCreateUserRequest(
    values: CreateUserFormValues,
    fixedTenantId?: string,
): CreateUserRequest {
    const tenantId = fixedTenantId ?? values.tenantId;
    return {
        email: values.email.trim(),
        firstName: values.firstName,
        lastName: values.lastName,
        role: values.role,
        isOwner: values.isOwner,
        ...(tenantId ? { tenantId } : {}),
    };
}

export function useCreateUser(options: UseCreateUserOptions = {}) {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { fixedTenantId, onSuccess, onError } = options;

    return useMutation({
        mutationFn: (values: CreateUserFormValues) =>
            createUser(toCreateUserRequest(values, fixedTenantId)),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.platform() });
            message.success(t('users.create.success'));
            onSuccess?.();
        },
        onError: () => {
            message.error(t('tenants.users.create.messages.failed'));
            onError?.();
        },
    });
}
