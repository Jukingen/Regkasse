'use client';

/**
 * Header badge: Mandantenlizenz (tenant row) only — never deployment / Server-Lizenz.
 * Unified read model: {@link useTenantLicense} (Super Admin → admin API; Manager → public status).
 */
import { Tooltip } from 'antd';

import { HeaderLicenseTooltipContent } from '@/features/tenant/components/HeaderLicenseTooltipContent';
import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import {
  getHeaderLicenseStatusClass,
  getHeaderLicenseStatusText,
  getHeaderLicenseTooltip,
  hasDetailedHeaderLicenseTooltip,
} from '@/features/tenant/utils/headerLicenseStatus';
import { useI18n } from '@/i18n';

export type LicenseStatusIndicatorProps = {
  compact?: boolean;
};

export function LicenseStatusIndicator({ compact: _compact = false }: LicenseStatusIndicatorProps) {
  const { t } = useI18n();
  const { mode, resolvedStatus, licenseValidUntilUtc, isLoading, isUnavailable } =
    useHeaderTenantLicense();

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

  const statusContext = { validUntilUtc: licenseValidUntilUtc };
  const statusClass = getHeaderLicenseStatusClass(resolvedStatus);
  const statusText = getHeaderLicenseStatusText(resolvedStatus, t, statusContext);
  const tooltipAriaLabel = getHeaderLicenseTooltip(resolvedStatus, t, statusContext);
  const showDetailedTooltip = hasDetailedHeaderLicenseTooltip(statusContext);

  return (
    <Tooltip
      title={
        showDetailedTooltip ? (
          <HeaderLicenseTooltipContent status={resolvedStatus} context={statusContext} t={t} />
        ) : (
          tooltipAriaLabel
        )
      }
      placement="bottom"
    >
      <div
        className={`license-badge ${statusClass} license-badge-tooltip-trigger`}
        aria-label={tooltipAriaLabel}
      >
        <span className="license-text">{statusText}</span>
      </div>
    </Tooltip>
  );
}

/** @deprecated Use `LicenseStatusIndicator` */
export const LicenseStatusBadge = LicenseStatusIndicator;
