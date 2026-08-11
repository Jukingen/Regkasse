import type { LicenseActivationResultDto } from '../api/license';
import type { LicenseStatus } from '../services/license/licenseStatusCache';
import { normalizeLicenseDaysRemaining } from './licenseExpiryRemaining';

/**
 * Builds an immediate POS license snapshot from POST /api/license/activate
 * so header/modal update before overview refetch completes.
 */
export function licenseStatusFromActivationResult(
  res: LicenseActivationResultDto,
  prev: LicenseStatus | null
): LicenseStatus {
  const expiry =
    typeof res.validUntil === 'string' && res.validUntil.trim().length > 0
      ? res.validUntil.trim()
      : null;

  let daysRemaining: number;
  if (typeof res.daysRemaining === 'number' && Number.isFinite(res.daysRemaining)) {
    daysRemaining = Math.max(0, normalizeLicenseDaysRemaining(res.daysRemaining));
  } else if (expiry) {
    const ms = new Date(expiry).getTime() - Date.now();
    daysRemaining = Number.isFinite(ms) ? Math.max(0, Math.ceil(ms / 86_400_000)) : 0;
  } else {
    daysRemaining = prev?.daysRemaining ?? 0;
  }

  const licenseType =
    typeof res.licenseType === 'string' && res.licenseType.trim().length > 0
      ? res.licenseType.trim()
      : 'Licensed';

  return {
    isValid: true,
    isTrial: false,
    isExpired: false,
    daysRemaining,
    expiryDate: expiry,
    machineHash: prev?.machineHash ?? '',
    licenseType,
    mode: 'Production',
    enabledFeatures: prev?.enabledFeatures ?? null,
  };
}
