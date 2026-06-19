'use client';

/**
 * Header badge: Mandantenlizenz (tenant row) only — never deployment / Server-Lizenz.
 */

import { Tooltip } from 'antd';

import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import {
    getHeaderLicenseStatusClass,
    getHeaderLicenseStatusText,
    getHeaderLicenseTooltip,
} from '@/features/tenant/utils/headerLicenseStatus';
import { useI18n } from '@/i18n';

export type LicenseStatusIndicatorProps = {
    compact?: boolean;
};

export function LicenseStatusIndicator({ compact: _compact = false }: LicenseStatusIndicatorProps) {
    const { t } = useI18n();
    const { mode, license, licenseValidUntilUtc, isLoading } = useHeaderTenantLicense();

    if (mode === 'hidden') {
        return null;
    }

    if (isLoading) {
        return (
            <div className="license-badge loading" aria-busy="true" aria-live="polite">
                <span className="license-text">{t('license.badge.loading')}</span>
            </div>
        );
    }

    if (!license) {
        return null;
    }

    const statusClass = getHeaderLicenseStatusClass(license, licenseValidUntilUtc);
    const statusText = getHeaderLicenseStatusText(license, t, licenseValidUntilUtc);
    const tooltip = getHeaderLicenseTooltip(license, t, licenseValidUntilUtc);

    return (
        <Tooltip title={tooltip}>
            <div className={`license-badge ${statusClass} license-badge-tooltip-trigger`} aria-label={tooltip}>
                <span className="license-text">{statusText}</span>
            </div>
        </Tooltip>
    );
}

/** @deprecated Use `LicenseStatusIndicator` */
export const LicenseStatusBadge = LicenseStatusIndicator;
