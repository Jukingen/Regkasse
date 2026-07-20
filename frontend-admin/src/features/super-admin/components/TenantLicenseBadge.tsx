'use client';

import React from 'react';
import { QuestionCircleOutlined } from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';

import { LicenseStatusBadge } from '@/features/tenants/components/LicenseStatusBadge';
import {
    clampTenantGraceRemaining,
    TENANT_GRACE_PERIOD_DAYS,
} from '@/features/license/constants/licenseGracePeriod';
import { useTenantLicenseStatus, type LicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import {
    getLicenseStatusMessage,
    resolveTenantRowLicenseStatus,
} from '@/features/license/utils/licenseStatus';
import { useI18n } from '@/i18n';

export type TenantLicenseBadgeProps = {
    tenantId?: string;
    licenseValidUntilUtc?: string | null;
    licenseKey?: string | null;
    licenseDaysRemaining?: number | null;
};

export function TenantLicenseBadge({
    tenantId,
    licenseValidUntilUtc,
    licenseKey,
    licenseDaysRemaining,
}: TenantLicenseBadgeProps) {
    const { t } = useI18n();
    const { data: remoteStatus } = useTenantLicenseStatus(tenantId);

    const fallbackStatus = resolveTenantRowLicenseStatus({
        licenseValidUntilUtc,
        licenseKey,
        licenseDaysRemaining,
    });
    const status: LicenseStatus = remoteStatus ?? {
        ...fallbackStatus,
        message: getLicenseStatusMessage(fallbackStatus, 'tenant', t),
    };

    if (status.kind === 'no_license' && !licenseValidUntilUtc?.trim() && !licenseKey?.trim()) {
        return (
            <Tooltip title={status.message}>
                <Tag color="default" icon={<QuestionCircleOutlined />}>
                    Keine Lizenz
                </Tag>
            </Tooltip>
        );
    }

    return (
        <LicenseStatusBadge
            validUntil={licenseValidUntilUtc ?? null}
            isInGracePeriod={status.kind === 'grace_write'}
            isLockdown={status.kind === 'lockdown'}
            daysRemaining={status.daysRemaining}
            gracePeriodRemaining={
                status.kind === 'grace_write'
                    ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - status.daysExpired)
                    : undefined
            }
        />
    );
}
