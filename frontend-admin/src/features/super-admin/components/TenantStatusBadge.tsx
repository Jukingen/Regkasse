'use client';

import React from 'react';

import { StatusBadge, resolveStatusType } from '@/components/StatusBadge';
import { useI18n } from '@/i18n';

export type TenantStatusBadgeProps = {
    status: string;
};

export function TenantStatusBadge({ status }: TenantStatusBadgeProps) {
    const { t } = useI18n();
    const resolved = resolveStatusType(status);
    const normalized = status?.toLowerCase() ?? '';
    const tenantLabelKey =
        normalized === 'active' || normalized === 'suspended' || normalized === 'deleted'
            ? (`tenants.status.${normalized}` as const)
            : null;

    if (resolved) {
        return (
            <StatusBadge
                status={resolved}
                label={tenantLabelKey ? t(tenantLabelKey) : undefined}
            />
        );
    }

    return <StatusBadge status="info" label={status || '—'} />;
}
