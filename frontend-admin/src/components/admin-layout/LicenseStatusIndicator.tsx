'use client';

/**
 * Mandant (tenant) SaaS license in header — Manager + tenant context only.
 * Super Admin platform mode is consolidated into TenantBadge.
 */

import { Tag, Tooltip } from 'antd';

import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import { mapTenantLicenseLabelToBadge } from '@/features/tenant/utils/mandantLicenseBadge';
import { useI18n } from '@/i18n';

export function LicenseStatusIndicator() {
    const { t } = useI18n();
    const { mode, license, isLoading } = useHeaderTenantLicense();

    if (mode !== 'tenant') {
        return null;
    }

    if (isLoading) {
        return (
            <Tag aria-busy="true" aria-live="polite">
                {t('license.badge.loading')}
            </Tag>
        );
    }

    if (!license || license.kind === 'none') {
        return null;
    }

    const display = mapTenantLicenseLabelToBadge(license, t);
    if (!display) {
        return null;
    }

    return (
        <Tooltip title={display.tooltip}>
            <Tag color={display.color}>{display.label}</Tag>
        </Tooltip>
    );
}

/** @deprecated Use `LicenseStatusIndicator` */
export const LicenseStatusBadge = LicenseStatusIndicator;
