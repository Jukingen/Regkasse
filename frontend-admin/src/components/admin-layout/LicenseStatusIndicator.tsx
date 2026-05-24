'use client';

/**
 * Mandant (tenant) SaaS license in header — Manager + tenant context only.
 * Super Admin platform mode is consolidated into TenantBadge.
 */

import { LoadingOutlined, SafetyOutlined } from '@ant-design/icons';
import { Tooltip } from 'antd';

import {
    getHeaderLicenseStatusClass,
    getHeaderLicenseStatusText,
} from '@/features/tenant/utils/headerLicenseStatus';
import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import { mapTenantLicenseLabelToBadge } from '@/features/tenant/utils/mandantLicenseBadge';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useI18n } from '@/i18n';

export type LicenseStatusIndicatorProps = {
    compact?: boolean;
};

export function LicenseStatusIndicator({ compact: _compact = false }: LicenseStatusIndicatorProps) {
    const { t } = useI18n();
    const { isSuperAdminPlatformMode, suppressLicenseWarnings } = useCurrentTenant();
    const { mode, license, isLoading } = useHeaderTenantLicense();

    if (suppressLicenseWarnings || isSuperAdminPlatformMode) {
        return null;
    }

    if (mode !== 'tenant') {
        return null;
    }

    if (isLoading) {
        return (
            <Tooltip
                title={t('license.badge.loading')}
                placement="bottom"
                mouseEnterDelay={0.2}
            >
                <span className="license-badge-tooltip-trigger">
                    <div className="license-badge loading" aria-busy="true" aria-live="polite">
                        <LoadingOutlined className="license-icon" spin aria-hidden />
                        <span className="license-text">{t('license.badge.loading')}</span>
                    </div>
                </span>
            </Tooltip>
        );
    }

    if (!license || license.kind === 'none') {
        return null;
    }

    const display = mapTenantLicenseLabelToBadge(license, t);
    if (!display) {
        return null;
    }

    const statusClass = getHeaderLicenseStatusClass(license);
    const statusText = getHeaderLicenseStatusText(license, t);

    return (
        <Tooltip title={display.tooltip} placement="bottom" mouseEnterDelay={0.2}>
            <span className="license-badge-tooltip-trigger">
                <div className={`license-badge ${statusClass}`} aria-label={statusText}>
                    <SafetyOutlined className="license-icon" aria-hidden />
                    <span className="license-text">{statusText}</span>
                </div>
            </span>
        </Tooltip>
    );
}

/** @deprecated Use `LicenseStatusIndicator` */
export const LicenseStatusBadge = LicenseStatusIndicator;
