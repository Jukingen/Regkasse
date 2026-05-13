import { useCallback, useEffect, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';

import { licenseApi, type LicensePublicStatusDto } from '../api/license';
import { apiClient } from '../services/api/config';
import { applyPersistedLicenseOverride } from '../utils/posLicenseLocalOverride';

/**
 * License snapshot consumed by the POS license-expiry banner and critical-action guards.
 * Primary fields come from GET /api/health/license; read-model fields from GET /api/license/status when available.
 * TODO(regkasse-license): Remove client-side AsyncStorage override in `applyPersistedLicenseOverride` once backend is fixed.
 */
export type LicenseStatus = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  /** ISO 8601 UTC; null when license has no exp claim. */
  expiryDate: string | null;
  machineHash: string;
  /** From GET /api/license/status (may be absent on older hosts). */
  licenseType?: string | null;
  /** From GET /api/license/status: Demo / Trial / Production. */
  mode?: string | null;
};

const POLL_MS = 10 * 60 * 1000;

/**
 * Fetches GET /api/health/license and GET /api/license/status (anonymous) on the same cadence.
 * On partial failure, keeps whatever snapshot is still usable; full failure yields null.
 */
export function useLicenseStatus() {
  const [status, setStatus] = useState<LicenseStatus | null>(null);
  const [loading, setLoading] = useState<boolean>(false);

  const fetchStatus = useCallback(async () => {
    setLoading(true);
    try {
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

      merged = await applyPersistedLicenseOverride(merged);
      setStatus(merged);
    } catch {
      setStatus(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchStatus();
    const id = setInterval(() => {
      void fetchStatus();
    }, POLL_MS);
    const onAppState = (next: AppStateStatus) => {
      if (next === 'active') {
        void fetchStatus();
      }
    };
    const sub = AppState.addEventListener('change', onAppState);
    return () => {
      clearInterval(id);
      sub.remove();
    };
  }, [fetchStatus]);

  return { status, loading, refetch: fetchStatus };
}

function normalizeHealth(raw: Record<string, unknown> | null | undefined): LicenseStatus | null {
  if (!raw || typeof raw !== 'object') return null;
  const daysRemainingRaw = raw.daysRemaining;
  const days = typeof daysRemainingRaw === 'number' && Number.isFinite(daysRemainingRaw)
    ? Math.max(0, Math.floor(daysRemainingRaw))
    : 0;
  return {
    isValid: raw.isValid === true,
    isTrial: raw.isTrial === true,
    isExpired: raw.isExpired === true,
    daysRemaining: days,
    expiryDate: typeof raw.expiryDate === 'string' && raw.expiryDate.length > 0 ? raw.expiryDate : null,
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

/** True when anonymous public DTO reflects a paid / production license (overrides stale health trial flags). */
function inferPaidFromPublic(p: LicensePublicStatusDto): boolean {
  const lt = (p.licenseType ?? '').trim().toLowerCase();
  const m = (p.mode ?? '').trim().toLowerCase();
  if (lt === 'licensed' || lt === 'paid') return true;
  if (m === 'production' && p.isValid === true && p.isExpired !== true) return true;
  return false;
}

function mergePublic(
  base: LicenseStatus | null,
  pub: LicensePublicStatusDto
): LicenseStatus | null {
  const publicDays =
    typeof pub.daysRemaining === 'number' && Number.isFinite(pub.daysRemaining)
      ? Math.max(0, Math.floor(pub.daysRemaining))
      : null;
  const publicExpired = pub.isExpired === true;
  const publicPaid = inferPaidFromPublic(pub);
  const mergedTrial = publicPaid ? false : inferTrialFromPublic(pub) || (base?.isTrial === true);
  const mergedValid = pub.isValid === true || base?.isValid === true;

  /** GET /api/license/status is the anonymous read model; do not OR stale /api/health/license isExpired into an active trial. */
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
      expiryDate: typeof pub.validUntil === 'string' && pub.validUntil.length > 0 ? pub.validUntil : null,
      machineHash: '',
      licenseType: pub.licenseType ?? null,
      mode: pub.mode ?? null,
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
      typeof pub.validUntil === 'string' && pub.validUntil.length > 0 ? pub.validUntil : base.expiryDate,
  };
}
