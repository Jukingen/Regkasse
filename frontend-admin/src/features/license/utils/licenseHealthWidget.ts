import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { getGracePeriodProgressPercent } from '@/features/license/utils/gracePeriodProgress';
import { getLicenseCountdownAccentColor } from '@/features/license/utils/licenseCountdownWidget';
import type { LicenseLifecycleUiState } from '@/hooks/useLicenseStatus';

export type LicenseHealthStatusInput = {
  state: LicenseLifecycleUiState;
  daysUntilExpiry: number;
  graceDaysRemaining: number;
  /** When false / missing expiry, progress should not imply a healthy year. */
  hasExpiry?: boolean;
};

/** Progress percent for the license health bar (Active year / Grace window / locked = 0). */
export function getLicenseHealthPercent(status: LicenseHealthStatusInput): number {
  if (status.hasExpiry === false) {
    return 0;
  }

  switch (status.state) {
    case 'Active':
      return Math.max(0, Math.min(100, (status.daysUntilExpiry / 365) * 100));
    case 'Grace':
      return getGracePeriodProgressPercent(
        status.graceDaysRemaining,
        TENANT_GRACE_PERIOD_DAYS
      );
    case 'Locked':
    case 'Archived':
    default:
      return 0;
  }
}

/**
 * Stroke color aligned with countdown thresholds (green / blue / amber / red).
 * Grace uses amber; Locked/Archived use red.
 */
export function getLicenseHealthStrokeColor(
  state: LicenseLifecycleUiState,
  daysLeft: number
): string {
  if (state === 'Locked' || state === 'Archived') {
    return '#cf1322';
  }
  if (state === 'Grace') {
    return '#faad14';
  }
  return getLicenseCountdownAccentColor(false, daysLeft);
}
