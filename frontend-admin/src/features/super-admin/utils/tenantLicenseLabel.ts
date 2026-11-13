import {
  calculateLicenseDaysRemaining,
  formatLicenseValidUntil,
} from '@/features/license/utils/licenseValidUntil';

export type TenantLicenseKind = 'none' | 'trial' | 'valid' | 'expired';

export type TenantLicenseLabel = {
  kind: TenantLicenseKind;
  /** de-AT short label for table cells */
  label: string;
  daysRemaining: number | null;
};

export function resolveTenantLicenseLabel(
  licenseValidUntilUtc: string | null | undefined,
  licenseKey: string | null | undefined,
  now = Date.now(),
  serverDaysRemaining?: number | null
): TenantLicenseLabel {
  const serverDays =
    typeof serverDaysRemaining === 'number' && Number.isFinite(serverDaysRemaining)
      ? Math.trunc(serverDaysRemaining)
      : null;

  const daysFromValidUntil = calculateLicenseDaysRemaining(licenseValidUntilUtc, now);
  const daysRemaining = daysFromValidUntil ?? serverDays;

  if (daysRemaining == null) {
    if (licenseKey?.trim()) {
      return { kind: 'valid', label: '—', daysRemaining: null };
    }
    return { kind: 'none', label: '—', daysRemaining: null };
  }

  if (daysRemaining < 0) {
    return { kind: 'expired', label: 'Abgelaufen', daysRemaining };
  }

  const isTrial = !licenseKey?.trim() || daysRemaining <= 31;
  if (isTrial) {
    return {
      kind: 'trial',
      label: `Demo (${daysRemaining} T.)`,
      daysRemaining,
    };
  }

  return {
    kind: 'valid',
    label: licenseValidUntilUtc?.trim() ? formatLicenseValidUntil(licenseValidUntilUtc) : '—',
    daysRemaining,
  };
}
