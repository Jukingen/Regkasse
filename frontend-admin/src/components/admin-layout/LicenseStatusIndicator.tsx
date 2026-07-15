'use client';

/**
 * Header badge: Mandantenlizenz (tenant row) only — never deployment / Server-Lizenz.
 * Unified read model: {@link useTenantLicense} (Super Admin → admin API; Manager → public status).
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
    const { mode, resolvedStatus, isLoading, isUnavailable } = useHeaderTenantLicense();

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

    if (isUnavailable || !resolvedStatus) {
        return (
            <Tooltip title={t('license.badge.unavailableTooltip')}>
                <div
                    className="license-badge expired license-badge-tooltip-trigger"
                    aria-label={t('license.badge.unavailable')}
                >
                    <span className="license-text">{t('license.badge.unavailable')}</span>
                </div>
            </Tooltip>
        );
    }

    const statusClass = getHeaderLicenseStatusClass(resolvedStatus);
    const statusText = getHeaderLicenseStatusText(resolvedStatus, t);
    const tooltip = getHeaderLicenseTooltip(resolvedStatus, t);

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
