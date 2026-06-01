'use client';

import React from 'react';
import { Space, Tag, Typography } from 'antd';

import type { AdminUserTenantMembership } from '@/features/users/api/users';
import { useI18n } from '@/i18n';

const { Text } = Typography;

export type UserTenantSummaryProps = {
    userRole?: string | null;
    memberships: AdminUserTenantMembership[];
    loading?: boolean;
};

/** Read-only mandant assignment display for user detail/edit drawers. */
export function UserTenantSummary({ userRole, memberships, loading }: UserTenantSummaryProps) {
    const { t } = useI18n();

    if (loading) {
        return <Text type="secondary">{t('users.tenants.loading')}</Text>;
    }

    if (userRole === 'SuperAdmin') {
        return <Tag color="purple">{t('users.tenants.platformSuperAdmin')}</Tag>;
    }

    if (memberships.length === 0) {
        return <Text type="secondary">{t('users.tenants.platformOnly')}</Text>;
    }

    return (
        <Space orientation="vertical" size={4} style={{ width: '100%' }}>
            {memberships.map((m) => (
                <div key={m.tenantId}>
                    <Text strong>{m.tenantName}</Text>
                    <Text type="secondary" style={{ marginLeft: 8 }}>
                        ({m.tenantSlug})
                    </Text>
                    {m.isOwner ? (
                        <Tag color="geekblue" style={{ marginLeft: 8 }}>
                            {t('users.tabs.tenant.ownerBadge')}
                        </Tag>
                    ) : null}
                </div>
            ))}
        </Space>
    );
}
