import { secureStorage } from '../services/secureStorage';

// TODO(regkasse-license): Remove this client-side override once backend license/trial responses are authoritative.

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

const LICENSE_KEY_PATTERN = /^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$/i;

/**
 * Temporary: persist operator-entered license key + expiry (ISO 8601 UTC) for POS-only override of trial UI.
 * TODO(regkasse-license): Remove when backend status is trusted.
 */
export async function persistPosLicenseLocalOverride(
  licenseKey: string,
  expiryIsoUtc: string
): Promise<void> {
  const k = licenseKey.trim().toUpperCase();
  if (!LICENSE_KEY_PATTERN.test(k)) return;
  const exp = expiryIsoUtc.trim();
  if (!exp) return;
  await secureStorage.multiSet([
    [POS_LICENSE_OVERRIDE_KEY_STORAGE, k],
    [POS_LICENSE_OVERRIDE_EXPIRY_STORAGE, exp],
  ]);
}

/**
 * When a non-expired REGK + expiry exist locally, force Licensed/Production and ignore backend trial flags.
 * TODO(regkasse-license): Remove when backend status is trusted.
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
    if (!LICENSE_KEY_PATTERN.test(key)) return merged;

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
