import type { LicenseSaleResponse } from '@/api/generated/model';
import { LICENSE_SALE_PLAN_VALUES } from '@/features/billing/constants/licensePlans';

/** Licenses valid more than this many days are shown as unlimited. */
export const LICENSE_DAYS_UNLIMITED_THRESHOLD = 5 * 365;

/** Long-term / gray band: more than 2 years remaining. */
export const LICENSE_DAYS_LONG_TERM_THRESHOLD = 2 * 365;

export type LicenseValidityHealth =
  | 'longTerm'
  | 'healthy'
  | 'warning'
  | 'critical'
  | 'expired'
  | 'unknown';

type TranslateFn = (key: string, options?: Record<string, string | number>) => string;

export function formatLicensePlanLabel(
  plan: string | null | undefined,
  t: (key: string) => string
): string {
  switch (plan) {
    case LICENSE_SALE_PLAN_VALUES.sixMonths:
      return t('billing.plans.sixMonths');
    case LICENSE_SALE_PLAN_VALUES.twelveMonths:
      return t('billing.plans.twelveMonths');
    case LICENSE_SALE_PLAN_VALUES.custom:
      return t('billing.plans.custom');
    default:
      return plan ?? '—';
  }
}

export function formatSaleStatusLabel(
  status: string | null | undefined,
  t: (key: string) => string
): string {
  switch (status) {
    case 'active':
      return t('billing.status.active');
    case 'cancelled':
      return t('billing.status.cancelled');
    case 'refunded':
      return t('billing.status.refunded');
    case 'expired':
      return t('billing.status.expired');
    default:
      return status ?? '—';
  }
}

export function isSaleCancellable(sale: Pick<LicenseSaleResponse, 'status'>): boolean {
  return sale.status === 'active';
}

function startOfUtcDayMs(date: Date): number {
  return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

/**
 * Calendar-day difference between valid-until (UTC date) and today (UTC date).
 * Positive = remaining, 0 = ends today, negative = overdue.
 */
export function computeLicenseDaysRemaining(
  validUntilUtc: string | null | undefined,
  now: Date = new Date()
): number | null {
  if (!validUntilUtc) return null;
  const end = new Date(validUntilUtc);
  if (Number.isNaN(end.getTime())) return null;
  const msPerDay = 24 * 60 * 60 * 1000;
  return Math.trunc((startOfUtcDayMs(end) - startOfUtcDayMs(now)) / msPerDay);
}

export function isLicenseDaysUnlimited(days: number): boolean {
  return days > LICENSE_DAYS_UNLIMITED_THRESHOLD;
}

/**
 * Visual health band for validity cells.
 * - gray longTerm: > 2 years
 * - green healthy: > 30 days (and ≤ 2 years)
 * - yellow warning: 8–30 days
 * - orange critical: 0–7 days (ends today through 7 days; overlaps “7–30” → critical wins)
 * - red expired: < 0
 */
export function resolveLicenseValidityHealth(
  validUntilUtc: string | null | undefined,
  now: Date = new Date()
): LicenseValidityHealth {
  const days = computeLicenseDaysRemaining(validUntilUtc, now);
  if (days == null) return 'unknown';
  if (days > LICENSE_DAYS_LONG_TERM_THRESHOLD) return 'longTerm';
  if (days > 30) return 'healthy';
  if (days > 7) return 'warning';
  if (days >= 0) return 'critical';
  return 'expired';
}

/** Ant Design `Tag` color for a health band. */
export function licenseValidityHealthTagColor(
  health: LicenseValidityHealth
): string | undefined {
  switch (health) {
    case 'longTerm':
      return 'default';
    case 'healthy':
      return 'success';
    case 'warning':
      return 'gold';
    case 'critical':
      return 'orange';
    case 'expired':
      return 'error';
    default:
      return undefined;
  }
}

export function formatLicenseValidityTooltip(
  validUntilUtc: string | null | undefined,
  t: TranslateFn,
  now: Date = new Date()
): string | undefined {
  const days = computeLicenseDaysRemaining(validUntilUtc, now);
  if (days == null) return undefined;

  const health = resolveLicenseValidityHealth(validUntilUtc, now);

  if (health === 'longTerm' || isLicenseDaysUnlimited(days)) {
    return t('billing.licenseSales.healthTooltip.longTerm');
  }
  if (days < 0) {
    return t('billing.licenseSales.healthTooltip.expired', { days: Math.abs(days) });
  }
  if (days === 0) {
    return t('billing.licenseSales.healthTooltip.expiresToday');
  }
  if (days === 1) {
    return t('billing.licenseSales.healthTooltip.expiresInOne', { days });
  }
  return t('billing.licenseSales.healthTooltip.expiresIn', { days });
}

/** Numeric sort key: overdue < ends today < remaining < unlimited / missing. */
export function getLicenseDaysRemainingSortValue(
  validUntilUtc: string | null | undefined,
  now: Date = new Date()
): number {
  const days = computeLicenseDaysRemaining(validUntilUtc, now);
  if (days == null || isLicenseDaysUnlimited(days)) {
    return Number.POSITIVE_INFINITY;
  }
  return days;
}

export function formatLicenseDaysRemainingLabel(
  validUntilUtc: string | null | undefined,
  t: TranslateFn,
  now: Date = new Date()
): string {
  const days = computeLicenseDaysRemaining(validUntilUtc, now);
  if (days == null) return '—';
  if (isLicenseDaysUnlimited(days)) {
    return t('billing.licenseSales.unlimited');
  }
  if (days === 0) {
    return t('billing.licenseSales.endsToday');
  }
  if (days < 0) {
    return t('billing.licenseSales.overdue', { days: Math.abs(days) });
  }
  // Singular/plural: locales may use one template; EN uses One when days === 1.
  const formatKey =
    days === 1
      ? 'billing.licenseSales.daysRemainingFormatOne'
      : 'billing.licenseSales.daysRemainingFormat';
  return t(formatKey, { days });
}
