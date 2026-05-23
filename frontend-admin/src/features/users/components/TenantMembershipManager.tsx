'use client';

import React, { useEffect, useMemo, useState } from 'react';
import { Button, Checkbox, Modal, Space, Typography, message } from 'antd';
import { TeamOutlined } from '@ant-design/icons';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
    adminUsersQueryKeys,
    updateUserTenants,
    type AdminUserTenantMembership,
} from '@/features/users/api/users';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { useI18n } from '@/i18n';

export type TenantMembershipManagerProps = {
    userId: string;
    currentTenants: Array<{ id: string; name: string; role: string }>;
    onSuccess: () => void;
};

/** Super Admin: assign or remove business-tenant memberships for a user. */
export function TenantMembershipManager({
    userId,
    currentTenants,
    onSuccess,
}: TenantMembershipManagerProps) {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { tenants, isLoading: tenantsLoading } = useTenantList();
    const [open, setOpen] = useState(false);
    const [selected, setSelected] = useState<string[]>([]);

    const activeIds = useMemo(
        () => new Set(currentTenants.map((row) => row.id)),
        [currentTenants],
    );

    useEffect(() => {
        if (!open) return;
        setSelected(currentTenants.map((row) => row.id));
    }, [open, currentTenants]);

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

    const toggle = (tenantId: string, checked: boolean) => {
        setSelected((prev) =>
            checked ? [...new Set([...prev, tenantId])] : prev.filter((id) => id !== tenantId),
        );
    };

    return (
        <>
            <Button icon={<TeamOutlined />} onClick={() => setOpen(true)}>
                {t('users.tenants.manageAction')}
            </Button>
            <Modal
                title={t('users.tenants.manageTitle')}
                open={open}
                onCancel={() => setOpen(false)}
                onOk={() => saveMutation.mutate(selected)}
                confirmLoading={saveMutation.isPending}
                okText={t('users.tenants.manageSave')}
                cancelText={t('users.tenants.manageCancel')}
                width={520}
                destroyOnHidden
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
                    {t('users.tenants.manageHint')}
                </Typography.Paragraph>
                <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    {tenantsLoading ? (
                        <Typography.Text type="secondary">{t('users.tenants.loading')}</Typography.Text>
                    ) : (
                        tenants.map((tenant) => (
                            <Checkbox
                                key={tenant.id}
                                checked={selected.includes(tenant.id)}
                                onChange={(e) => toggle(tenant.id, e.target.checked)}
                            >
                                {tenant.name}{' '}
                                <Typography.Text type="secondary">({tenant.slug})</Typography.Text>
                                {activeIds.has(tenant.id) ? (
                                    <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                                        — {currentTenants.find((c) => c.id === tenant.id)?.role}
                                    </Typography.Text>
                                ) : null}
                            </Checkbox>
                        ))
                    )}
                </Space>
            </Modal>
        </>
    );
}

export function membershipsToManagerRows(
    memberships: AdminUserTenantMembership[],
): Array<{ id: string; name: string; role: string }> {
    return memberships.map((m) => ({
        id: m.tenantId,
        name: m.tenantName,
        role: m.role,
    }));
}
