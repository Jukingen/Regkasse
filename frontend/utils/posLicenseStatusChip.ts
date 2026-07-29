import type { MandantLicenseWarningState } from '../types/mandantLicenseWarning';
import type { LicenseStatus } from '../services/license/licenseStatusCache';

export type PosLicenseChipState = 'Active' | 'Grace' | 'Locked';

export type PosLicenseChipModel = {
  state: PosLicenseChipState;
  /** Days shown in the chip message (remaining, grace, or overdue). */
  displayDays: number;
  /** ISO expiry / valid-until when known. */
  expiresAt: string | null;
};

export type MapPosLicenseChipInput = {
  mandant: MandantLicenseWarningState | null;
  /** From deriveMandantWarningFlags — prefer over raw isInGracePeriod alone. */
  shouldShowGrace?: boolean;
  deployment: LicenseStatus | null;
};

/**
 * Maps mandant warning + deployment license snapshot into a compact POS chip model.
 */
export function mapPosLicenseChipState(input: MapPosLicenseChipInput): PosLicenseChipModel {
  const { mandant, deployment } = input;
  const shouldShowGrace =
    input.shouldShowGrace === true ||
    (mandant?.isInGracePeriod === true && (mandant.gracePeriodRemaining ?? 0) >= 0);

  if (shouldShowGrace && mandant) {
    return {
      state: 'Grace',
      displayDays: Math.max(0, mandant.gracePeriodRemaining),
      expiresAt: mandant.validUntil ?? deployment?.expiryDate ?? null,
    };
  }

  const mandantLocked =
    mandant != null && (mandant.isLocked === true || mandant.canAccess === false);
  const deploymentExpiredWithoutGrace = deployment?.isExpired === true && !shouldShowGrace;

  if (mandantLocked || deploymentExpiredWithoutGrace) {
    return {
      state: 'Locked',
      displayDays: Math.max(0, mandant?.daysOverdue ?? 0),
      expiresAt: mandant?.validUntil ?? deployment?.expiryDate ?? null,
    };
  }

  const daysRemaining =
    mandant != null && typeof mandant.daysRemaining === 'number'
      ? Math.max(0, mandant.daysRemaining)
      : Math.max(0, deployment?.daysRemaining ?? 0);

  return {
    state: 'Active',
    displayDays: daysRemaining,
    expiresAt: mandant?.validUntil ?? deployment?.expiryDate ?? null,
  };
}

export function posLicenseChipColor(state: PosLicenseChipState): string {
  switch (state) {
    case 'Active':
      return '#52c41a';
    case 'Grace':
      return '#faad14';
    case 'Locked':
      return '#cf1322';
  }
}
