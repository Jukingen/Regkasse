import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import type { LicenseLifecycleUiState } from '@/hooks/useLicenseStatus';

export type LicenseImpactAccent = 'ok' | 'warn' | 'danger';

export type LicenseExpiryImpactModel = {
  currentAccent: LicenseImpactAccent;
  graceAccent: LicenseImpactAccent;
  lockedAccent: LicenseImpactAccent;
  alertType: 'info' | 'warning' | 'error';
  /** Days shown under the “current” tile (until expiry while Active, grace remaining, or overdue). */
  currentDaysLabelValue: number;
  currentDaysKind: 'untilExpiry' | 'graceRemaining' | 'overdue';
};

const ACCENT_BORDER: Record<LicenseImpactAccent, string> = {
  ok: '#b7eb8f',
  warn: '#ffe58f',
  danger: '#ffa39e',
};

const ACCENT_BG: Record<LicenseImpactAccent, string> = {
  ok: '#f6ffed',
  warn: '#fffbe6',
  danger: '#fff1f0',
};

export function getLicenseImpactAccentStyles(accent: LicenseImpactAccent): {
  borderColor: string;
  background: string;
} {
  return {
    borderColor: ACCENT_BORDER[accent],
    background: ACCENT_BG[accent],
  };
}

/**
 * Maps mandant lifecycle → three-phase impact accents (Active → Grace → Locked).
 * Grace remains full access with warnings (not read-only); Locked is FA read-only / POS blocked.
 */
export function getLicenseExpiryImpactModel(input: {
  state: LicenseLifecycleUiState;
  daysUntilExpiry: number;
  graceDaysRemaining: number;
  daysOverdue: number;
}): LicenseExpiryImpactModel {
  const { state, daysUntilExpiry, graceDaysRemaining, daysOverdue } = input;

  if (state === 'Locked' || state === 'Archived') {
    return {
      currentAccent: 'danger',
      graceAccent: 'danger',
      lockedAccent: 'danger',
      alertType: 'error',
      currentDaysLabelValue: Math.max(0, daysOverdue),
      currentDaysKind: 'overdue',
    };
  }

  if (state === 'Grace') {
    return {
      currentAccent: 'warn',
      graceAccent: 'warn',
      lockedAccent: graceDaysRemaining <= 1 ? 'danger' : 'warn',
      alertType: 'error',
      currentDaysLabelValue: Math.max(0, graceDaysRemaining),
      currentDaysKind: 'graceRemaining',
    };
  }

  // Active
  const days = Math.max(0, daysUntilExpiry);
  const approaching = days <= TENANT_GRACE_PERIOD_DAYS;
  return {
    currentAccent: approaching ? 'warn' : 'ok',
    graceAccent: approaching ? 'warn' : 'ok',
    lockedAccent: days <= 14 ? 'warn' : 'ok',
    alertType: approaching ? 'warning' : 'info',
    currentDaysLabelValue: days,
    currentDaysKind: 'untilExpiry',
  };
}
