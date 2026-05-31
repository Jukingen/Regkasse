'use client';

import React from 'react';
import { CheckCircleOutlined, CloseCircleOutlined, StopOutlined, WarningOutlined } from '@ant-design/icons';
import { Tag, Tooltip } from 'antd';

import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '@/features/license/constants/licenseGracePeriod';

const DAY_MS = 1000 * 60 * 60 * 24;

export type LicenseStatusBadgeProps = {
    validUntil: string | null;
    isInGracePeriod?: boolean;
    daysRemaining?: number;
    gracePeriodRemaining?: number;
    /** When true, show lockdown state instead of generic expired. */
    isLockdown?: boolean;
};

export function LicenseStatusBadge({
    validUntil,
    isInGracePeriod,
    daysRemaining,
    gracePeriodRemaining,
    isLockdown,
}: LicenseStatusBadgeProps) {
    if (!validUntil) {
        return <Tag color="green">Aktiv (unbegrenzt)</Tag>;
    }

    const expiryDate = new Date(validUntil);
    const today = new Date();
    const daysLeft =
        typeof daysRemaining === 'number' && Number.isFinite(daysRemaining)
            ? Math.trunc(daysRemaining)
            : Math.ceil((expiryDate.getTime() - today.getTime()) / DAY_MS);

    if (isInGracePeriod) {
        const overdueDays = Math.abs(daysLeft);
        const graceLeft =
            typeof gracePeriodRemaining === 'number' && Number.isFinite(gracePeriodRemaining)
                ? Math.max(0, gracePeriodRemaining)
                : undefined;

        return (
            <Tooltip
                title={
                    graceLeft != null
                        ? `Grace Period: noch ${graceLeft} Tage`
                        : 'Grace Period: Lizenz abgelaufen, Verlängerung empfohlen'
                }
            >
                <Tag color="orange" icon={<WarningOutlined />}>
                    Grace Period ({overdueDays} Tage überfällig)
                </Tag>
            </Tooltip>
        );
    }

    if (isLockdown) {
        return (
            <Tooltip title="Lizenz abgelaufen. Zugang gesperrt. Bitte verlängern.">
                <Tag color="red" icon={<StopOutlined />}>
                    Gesperrt
                </Tag>
            </Tooltip>
        );
    }

    if (daysLeft < 0) {
        return (
            <Tooltip title="Lizenz abgelaufen. Bitte verlängern.">
                <Tag color="red" icon={<CloseCircleOutlined />}>
                    Abgelaufen
                </Tag>
            </Tooltip>
        );
    }

    if (daysLeft <= TENANT_WARNING_DAYS_BEFORE_EXPIRY) {
        return (
            <Tooltip title={`Lizenz läuft in ${daysLeft} Tagen ab`}>
                <Tag color="orange" icon={<WarningOutlined />}>
                    Läuft bald ab ({daysLeft} Tage)
                </Tag>
            </Tooltip>
        );
    }

    return (
        <Tooltip title={`Gültig bis ${expiryDate.toLocaleDateString('de-DE')}`}>
            <Tag color="green" icon={<CheckCircleOutlined />}>
                Aktiv ({daysLeft} Tage)
            </Tag>
        </Tooltip>
    );
}
