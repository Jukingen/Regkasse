'use client';

import React, { useState } from 'react';
import { Button, Space, Typography, message } from 'antd';
import { UserAddOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { InviteUserModal } from '@/features/users/components/InviteUserModal';
import type { InviteUserFormValues } from '@/features/users/components/InviteUserModal';
import { SuperAdminCredentialsGate } from '@/features/super-admin/components/SuperAdminCredentialsGate';
import { UserCreatedSuccessModal } from '@/features/super-admin/components/UserCreatedSuccessModal';
import {
    createTenantUser,
    type CreateTenantUserResult,
} from '@/features/super-admin/api/tenantUsers';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { adminUsersQueryKeys } from '@/features/users/api/users';
import { useI18n } from '@/i18n';

/** Super Admin only — create mandant users with one-time password (no email). */
export function UserInvitationsPanel() {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const [inviteOpen, setInviteOpen] = useState(false);
    const [createResult, setCreateResult] = useState<CreateTenantUserResult | null>(null);

    const { tenants, isLoading } = useTenantList();

    const createMutation = useMutation({
        mutationFn: async (values: InviteUserFormValues) => {
            if (!values.tenantId) throw new Error('tenantId required');
            return createTenantUser(values.tenantId, {
                email: values.email.trim(),
                role: values.role,
                isOwner: values.isOwner,
            });
        },
        onSuccess: (res) => {
            setCreateResult(res);
            setInviteOpen(false);
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.tenant() });
            message.success(t('tenants.users.invite.messages.created'));
        },
        onError: () => message.error(t('tenants.users.invite.messages.failed')),
    });

    if (!canProvisionTenantCredentials) {
        return <SuperAdminCredentialsGate />;
    }

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.tabs.invitations.descriptionSuperAdmin')}
            </Typography.Paragraph>
            <SuperAdminCredentialsGate showRestrictedHint={false}>
                <Button type="primary" icon={<UserAddOutlined />} onClick={() => setInviteOpen(true)}>
                    {t('users.invite.action')}
                </Button>
            </SuperAdminCredentialsGate>
            <InviteUserModal
                open={inviteOpen}
                variant="usersPage"
                tenantRows={tenants}
                tenantsLoading={isLoading}
                confirmLoading={createMutation.isPending}
                onClose={() => setInviteOpen(false)}
                onSubmit={(values) => createMutation.mutate(values)}
            />
            <UserCreatedSuccessModal
                open={!!createResult}
                result={createResult}
                onClose={() => setCreateResult(null)}
            />
        </Space>
    );
}
