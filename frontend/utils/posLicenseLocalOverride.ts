import { isValidPosLicenseKey } from './licenseKeyFormat';
import { secureStorage } from '../services/secureStorage';

// Client-side override for offline POS when an operator pasted a REGK key.
// Backend GET /api/license/status is authoritative when online; do not expand this helper.

/** Same shape as `LicenseStatus` from `useLicenseStatus` (duplicated to avoid hook↔util import cycle). */
type MergedLicenseSnapshot = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  expiryDate: string | null;
  machineHash: string;
  licenseType?: string | null;
  mode?: string | null;
};

export const POS_LICENSE_OVERRIDE_KEY_STORAGE = 'regkasse.pos.licenseKey';
export const POS_LICENSE_OVERRIDE_EXPIRY_STORAGE = 'regkasse.pos.licenseExpiryIsoUtc';

/**
 * Persist operator-entered license key + expiry for offline POS UI only.
 */
export async function persistPosLicenseLocalOverride(
  licenseKey: string,
  expiryIsoUtc: string
): Promise<void> {
  const k = licenseKey.trim().toUpperCase();
  if (!isValidPosLicenseKey(k)) return;
  const exp = expiryIsoUtc.trim();
  if (!exp) return;
  await secureStorage.multiSet([
    [POS_LICENSE_OVERRIDE_KEY_STORAGE, k],
    [POS_LICENSE_OVERRIDE_EXPIRY_STORAGE, exp],
  ]);
}

/**
 * When a non-expired REGK + expiry exist locally, prefer them over a stale trial snapshot while offline.
 */
export async function applyPersistedLicenseOverride(
  merged: MergedLicenseSnapshot | null
): Promise<MergedLicenseSnapshot | null> {
  try {
    const [rawKey, rawExp] = await secureStorage.multiGet([
      POS_LICENSE_OVERRIDE_KEY_STORAGE,
      POS_LICENSE_OVERRIDE_EXPIRY_STORAGE,
    ]);
    const key = rawKey[1]?.trim() ?? '';
    const expIso = rawExp[1]?.trim() ?? '';
    if (!key || !expIso) return merged;
    if (!isValidPosLicenseKey(key)) return merged;

    const expiry = new Date(expIso);
    if (Number.isNaN(expiry.getTime()) || expiry.getTime() <= Date.now()) {
      return merged;
    }

    const daysRemaining = Math.max(0, Math.ceil((expiry.getTime() - Date.now()) / 86_400_000));

    return {
      isValid: true,
      isTrial: false,
      isExpired: false,
      daysRemaining,
      expiryDate: expiry.toISOString(),
      machineHash: merged?.machineHash ?? '',
      licenseType: 'Licensed',
      mode: 'Production',
    };
  } catch {
    return merged;
  }
}
