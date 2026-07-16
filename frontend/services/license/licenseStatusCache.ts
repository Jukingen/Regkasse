import { licenseApi, type LicensePublicStatusDto } from '../../api/license';
import { apiClient } from '../api/config';
import { applyPersistedLicenseOverride } from '../../utils/posLicenseLocalOverride';
import { POS_LICENSE_CACHE_MS } from '../../constants/posPollingIntervals';
import { normalizeLicenseDaysRemaining } from '../../utils/licenseExpiryRemaining';

/** Deployment license snapshot (health + public status merge). */
export type LicenseStatus = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  expiryDate: string | null;
  machineHash: string;
  licenseType?: string | null;
  mode?: string | null;
  enabledFeatures?: readonly string[] | null;
};

type LicenseStatusCacheEntry = {
  data: LicenseStatus | null;
  timestamp: number;
};

const licenseStatusCache: LicenseStatusCacheEntry = {
  data: null,
  timestamp: 0,
};

function normalizeHealth(raw: Record<string, unknown> | null | undefined): LicenseStatus | null {
  if (!raw || typeof raw !== 'object') return null;
  const daysRemainingRaw = raw.daysRemaining;
  const days =
    typeof daysRemainingRaw === 'number' && Number.isFinite(daysRemainingRaw)
      ? Math.max(0, normalizeLicenseDaysRemaining(daysRemainingRaw))
      : 0;
  return {
    isValid: raw.isValid === true,
    isTrial: raw.isTrial === true,
    isExpired: raw.isExpired === true,
    daysRemaining: days,
    expiryDate:
      typeof raw.expiryDate === 'string' && raw.expiryDate.length > 0 ? raw.expiryDate : null,
    machineHash: typeof raw.machineHash === 'string' ? raw.machineHash : '',
    licenseType: null,
    mode: null,
  };
}

function inferTrialFromPublic(p: LicensePublicStatusDto): boolean {
  const lt = (p.licenseType ?? '').trim().toLowerCase();
  const m = (p.mode ?? '').trim().toLowerCase();
  return lt === 'trial' || lt === 'demo' || m === 'trial' || m === 'demo';
}

function inferPaidFromPublic(p: LicensePublicStatusDto): boolean {
  const lt = (p.licenseType ?? '').trim().toLowerCase();
  const m = (p.mode ?? '').trim().toLowerCase();
  if (lt === 'licensed' || lt === 'paid') return true;
  if (m === 'production' && p.isValid === true && p.isExpired !== true) return true;
  return false;
}

function mergePublic(base: LicenseStatus | null, pub: LicensePublicStatusDto): LicenseStatus | null {
  const publicDays =
    typeof pub.daysRemaining === 'number' && Number.isFinite(pub.daysRemaining)
      ? Math.max(0, normalizeLicenseDaysRemaining(pub.daysRemaining))
      : null;
  const publicExpired = pub.isExpired === true;
  const publicPaid = inferPaidFromPublic(pub);
  const mergedTrial = publicPaid ? false : inferTrialFromPublic(pub) || base?.isTrial === true;
  const mergedValid = pub.isValid === true || base?.isValid === true;

  const operationalTrialFromPublic =
    inferTrialFromPublic(pub) && pub.isValid === true && !publicExpired;
  const mergedExpired = operationalTrialFromPublic
    ? false
    : publicPaid
      ? publicExpired
      : publicExpired || base?.isExpired === true;

  if (!base) {
    const days = publicDays ?? 0;
    return {
      isValid: mergedValid,
      isTrial: mergedTrial,
      isExpired: mergedExpired,
      daysRemaining: days,
      expiryDate:
        typeof pub.validUntil === 'string' && pub.validUntil.length > 0 ? pub.validUntil : null,
      machineHash: '',
      licenseType: pub.licenseType ?? null,
      mode: pub.mode ?? null,
      enabledFeatures: pub.features?.length ? [...pub.features] : null,
    };
  }

  return {
    ...base,
    isValid: mergedValid,
    licenseType: pub.licenseType ?? base.licenseType ?? null,
    mode: pub.mode ?? base.mode ?? null,
    daysRemaining: publicDays ?? base.daysRemaining,
    isExpired: mergedExpired,
    isTrial: mergedTrial,
    expiryDate:
      typeof pub.validUntil === 'string' && pub.validUntil.length > 0
        ? pub.validUntil
        : base.expiryDate,
    enabledFeatures:
      pub.features && pub.features.length > 0 ? [...pub.features] : base.enabledFeatures ?? null,
  };
}

export function isLicenseStatusCacheFresh(now = Date.now()): boolean {
  return (
    licenseStatusCache.data != null &&
    now - licenseStatusCache.timestamp < POS_LICENSE_CACHE_MS
  );
}

export function getCachedLicenseStatus(): LicenseStatus | null {
  return licenseStatusCache.data;
}

/** Seeds module cache from GET /api/pos/status/overview (avoids separate license HTTP calls). */
export function setCachedLicenseStatus(data: LicenseStatus | null, timestamp = Date.now()): void {
  licenseStatusCache.data = data;
  licenseStatusCache.timestamp = timestamp;
}

async function fetchLicenseStatusFromNetwork(): Promise<LicenseStatus | null> {
  const [healthRes, publicRes] = await Promise.allSettled([
    apiClient.get<Record<string, unknown>>('/health/license'),
    licenseApi.getStatus(),
  ]);

  let merged: LicenseStatus | null = null;
  if (healthRes.status === 'fulfilled') {
    merged = normalizeHealth(healthRes.value);
  }
  if (publicRes.status === 'fulfilled') {
    merged = mergePublic(merged, publicRes.value);
  } else if (!merged) {
    merged = null;
  }

  return applyPersistedLicenseOverride(merged);
}

/**
 * Returns cached license snapshot when fresh; otherwise fetches GET /health/license + GET /license/status.
 */
export async function resolveLicenseStatus(force = false): Promise<LicenseStatus | null> {
  const now = Date.now();
  if (!force && isLicenseStatusCacheFresh(now)) {
    return applyPersistedLicenseOverride(licenseStatusCache.data);
  }

  try {
    const merged = await fetchLicenseStatusFromNetwork();
    licenseStatusCache.data = merged;
    licenseStatusCache.timestamp = now;
    return merged;
  } catch {
    if (licenseStatusCache.data) {
      return applyPersistedLicenseOverride(licenseStatusCache.data);
    }
    return null;
  }
}

/** Clears module cache (e.g. after successful license activation). */
export function invalidateLicenseStatusCache(): void {
  licenseStatusCache.data = null;
  licenseStatusCache.timestamp = 0;
}
