'use client';

/**
 * Soft license-expiry warning shown across the admin shell.
 *
 * Visibility rules (driven by GET /api/admin/license/status):
 *   - daysRemaining ≤ 15 && > 0 (and not yet expired) → yellow Alert (Hinweis)
 *   - isExpired === true                              → red Alert (Block-Hinweis)
 *   - otherwise                                       → renders nothing (silent)
 *
 * Does NOT block any UI action — payment-time enforcement lives on the backend.
 * Refetches every 10 minutes so a warning that crosses the threshold appears without a reload.
 */

import { Alert } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { formatDate, useI18n } from '@/i18n';
import {
  getLicenseStatus,
  licenseQueryKeys,
  type LicenseStatusResponse,
} from '@/api/manual/adminLicense';

const WARNING_THRESHOLD_DAYS = 15;
const REFETCH_INTERVAL_MS = 10 * 60 * 1000; // 10 minutes

export function LicenseExpiryBanner() {
  const { t, formatLocale } = useI18n();

  const query = useQuery<LicenseStatusResponse>({
    queryKey: licenseQueryKeys.status,
    queryFn: getLicenseStatus,
    // Soft warning is non-critical UX: keep silent on auth/permission errors instead of throwing.
    retry: false,
    refetchInterval: REFETCH_INTERVAL_MS,
    refetchOnWindowFocus: true,
    staleTime: 60 * 1000,
  });

  // While loading or on error (e.g., user lacks SettingsView permission), render nothing — no banner is the safe default.
  if (!query.data) return null;

  const { isExpired, daysRemaining, expiryDate } = query.data;
  const formattedDate = expiryDate
    ? formatDate(expiryDate, formatLocale, { year: 'numeric', month: '2-digit', day: '2-digit' })
    : null;

  if (isExpired) {
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
