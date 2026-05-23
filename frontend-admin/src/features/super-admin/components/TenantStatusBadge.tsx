'use client';

import React from 'react';
import { Tag } from 'antd';

import { useI18n } from '@/i18n';
import { tenantStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';

export type TenantStatusBadgeProps = {
    status: string;
};

export function TenantStatusBadge({ status }: TenantStatusBadgeProps) {
    const { t } = useI18n();
    const normalized = status?.toLowerCase() ?? '';
    const labelKey =
        normalized === 'active' || normalized === 'suspended' || normalized === 'deleted'
            ? (`tenants.status.${normalized}` as const)
            : null;

    return (
        <Tag color={tenantStatusColor(status)}>
            {labelKey ? t(labelKey) : status}
        </Tag>
    );
}
