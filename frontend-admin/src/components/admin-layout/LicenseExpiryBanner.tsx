'use client';

/**
 * Soft tenant-license expiry warning for Manager mandant context.
 * Super Admin and deployment/on-premise license are intentionally excluded.
 */

import { Alert } from 'antd';

import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { formatDate, useI18n } from '@/i18n';

const WARNING_THRESHOLD_DAYS = 15;

export function LicenseExpiryBanner() {
    const { t, formatLocale } = useI18n();
    const { suppressLicenseWarnings } = useCurrentTenant();
    const { mode, license, licenseValidUntilUtc } = useHeaderTenantLicense();

    if (suppressLicenseWarnings || mode !== 'tenant' || !license || license.kind === 'none') {
        return null;
    }

    const daysRemaining = license.daysRemaining ?? 0;
    const formattedDate = licenseValidUntilUtc
        ? formatDate(licenseValidUntilUtc, formatLocale, {
              year: 'numeric',
              month: '2-digit',
              day: '2-digit',
          })
        : null;

    if (license.kind === 'expired' || daysRemaining < 0) {
        return (
            <Alert
                type="error"
                showIcon
                banner
                role="alert"
                style={{ marginBottom: 12 }}
                message={t('license.banner.expired.title')}
                description={
                    formattedDate
                        ? t('license.banner.expired.messageWithDate', { date: formattedDate })
                        : t('license.banner.expired.message')
                }
            />
        );
    }

    if (daysRemaining > 0 && daysRemaining <= WARNING_THRESHOLD_DAYS) {
        return (
            <Alert
                type="warning"
                showIcon
                banner
                role="status"
                style={{ marginBottom: 12 }}
                message={t('license.banner.warning.title')}
                description={
                    formattedDate
                        ? t('license.banner.warning.messageWithDate', {
                              days: daysRemaining,
                              date: formattedDate,
                          })
                        : t('license.banner.warning.message', { days: daysRemaining })
                }
            />
        );
    }

    return null;
}
