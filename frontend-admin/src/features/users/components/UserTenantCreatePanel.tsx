'use client';

import React, { useState } from 'react';
import { Button, Space, Typography } from 'antd';
import { UserAddOutlined } from '@ant-design/icons';

import { CreateUserModal } from '@/features/users/components/CreateUserModal';
import { SuperAdminCredentialsGate } from '@/features/super-admin/components/SuperAdminCredentialsGate';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useCreateUser } from '@/features/users/hooks/useCreateUser';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useI18n } from '@/i18n';

/** Super Admin — create mandant users with one-time password (shown once, no email). */
export function UserTenantCreatePanel() {
    const { t } = useI18n();
    const { user } = useAuth();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const [createOpen, setCreateOpen] = useState(false);

    const { tenants, isLoading } = useTenantList();

    const createMutation = useCreateUser({
        onSuccess: () => setCreateOpen(false),
    });

    if (!canProvisionTenantCredentials) {
        return <SuperAdminCredentialsGate />;
    }

    return (
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                {t('users.tabs.tenantCreate.descriptionSuperAdmin')}
            </Typography.Paragraph>
            <SuperAdminCredentialsGate showRestrictedHint={false}>
                <Button type="primary" icon={<UserAddOutlined />} onClick={() => setCreateOpen(true)}>
                    {t('users.create.action')}
                </Button>
            </SuperAdminCredentialsGate>
            <CreateUserModal
                open={createOpen}
                variant="usersPage"
                isSuperAdmin={isSuperAdmin(user?.role)}
                tenantRows={tenants}
                tenantsLoading={isLoading}
                confirmLoading={createMutation.isPending}
                onClose={() => setCreateOpen(false)}
                onComplete={() => setCreateOpen(false)}
                onSubmit={(values) => createMutation.mutateAsync(values)}
            />
        </Space>
    );
}
