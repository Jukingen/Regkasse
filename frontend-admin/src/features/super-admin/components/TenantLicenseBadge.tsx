'use client';

import React from 'react';
import {
    CheckCircleOutlined,
    CloseCircleOutlined,
    QuestionCircleOutlined,
    StopOutlined,
    WarningOutlined,
} from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';

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

    const remainingGraceWriteDays = Math.max(0, 30 - status.daysExpired);
    const remainingLockdownDays = Math.max(0, 90 - status.daysExpired);

    const badgeConfig = {
        active: {
            color: 'green',
            text: 'Aktiv',
            icon: <CheckCircleOutlined />,
            tooltip: status.message,
        },
        grace_write: {
            color: 'orange',
            text: `Grace (${remainingGraceWriteDays} Tage)`,
            icon: <WarningOutlined />,
            tooltip: status.message,
        },
        grace_readonly: {
            color: 'red',
            text: `Verkaeufe deaktiviert (${remainingLockdownDays} Tage)`,
            icon: <CloseCircleOutlined />,
            tooltip: status.message,
        },
        lockdown: {
            color: 'red',
            text: 'Lockdown',
            icon: <StopOutlined />,
            tooltip: `${status.message} Nur Super Admin zugaenglich.`,
        },
        expired: {
            color: 'red',
            text: `Abgelaufen (${status.daysExpired} Tage)`,
            icon: <CloseCircleOutlined />,
            tooltip: status.message,
        },
        no_license: {
            color: 'default',
            text: 'Keine Lizenz',
            icon: <QuestionCircleOutlined />,
            tooltip: status.message,
        },
    } as const;

    const config = badgeConfig[status.kind];

    return (
        <Tooltip title={config.tooltip}>
            <Tag color={config.color} icon={config.icon}>
                {config.text}
            </Tag>
        </Tooltip>
    );
}
