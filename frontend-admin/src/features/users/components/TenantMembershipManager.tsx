'use client';

import { useAntdApp } from '@/hooks/useAntdApp';
import React, { useMemo, useState } from 'react';
import { Button } from 'antd';
import { TeamOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
    adminUsersQueryKeys,
    updateUserTenants,
    type AdminUserTenantMembership,
} from '@/features/users/api/users';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useI18n } from '@/i18n';
import {
    UserTenantAssignmentModal,
    type UserTenantAssignmentRow,
} from '@/features/users/components/UserTenantAssignmentModal';

export type TenantMembershipManagerProps = {
    userId: string;
    userEmail?: string;
    currentTenants: UserTenantAssignmentRow[];
    onSuccess: () => void;
};

/** Super Admin: assign or remove business-tenant memberships for a user. */
export function TenantMembershipManager({
    userId,
    userEmail = '',
    currentTenants,
    onSuccess,
}: TenantMembershipManagerProps) {
  const { message } = useAntdApp();

    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { tenants, isLoading: tenantsLoading } = useTenantList();
    const [open, setOpen] = useState(false);
    const hasExistingTenants = currentTenants.length > 0;
    const activeTenants = useMemo(
        () => tenants.filter((tenant) => tenant.isActive && tenant.status === 'active'),
        [tenants],
    );
    const canOpenModal = tenantsLoading || hasExistingTenants || activeTenants.length > 0;

    const saveMutation = useMutation({
        mutationFn: (tenantIds: string[]) => updateUserTenants(userId, tenantIds),
        onSuccess: () => {
            message.success(t('users.tenants.manageSaved'));
            setOpen(false);
            void queryClient.invalidateQueries({ queryKey: adminUsersQueryKeys.userTenants(userId) });
            void queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
            onSuccess();
        },
        onError: () => message.error(t('users.tenants.manageFailed')),
    });

    const handleOpen = () => {
        if (!canOpenModal) return;
        setOpen(true);
    };

    return (
        <>
            <Button
                icon={<TeamOutlined />}
                onClick={handleOpen}
                disabled={!canOpenModal && !tenantsLoading}
            >
                {t('users.tenants.manageAction')}
            </Button>
            {!tenantsLoading ? (
                <UserTenantAssignmentModal
                    open={open}
                    userEmail={userEmail}
                    currentTenants={currentTenants}
                    allTenants={activeTenants}
                    confirmLoading={saveMutation.isPending}
                    onClose={() => setOpen(false)}
                    onSave={(tenantIds) => saveMutation.mutate(tenantIds)}
                />
            ) : null}
        </>
    );
}

export function membershipsToManagerRows(
    memberships: AdminUserTenantMembership[],
): Array<{ id: string; name: string; slug: string; role: string; isOwner: boolean }> {
    return memberships.map((m) => ({
        id: m.tenantId,
        name: m.tenantName,
        slug: m.tenantSlug,
        role: m.role,
        isOwner: m.isOwner,
    }));
}
