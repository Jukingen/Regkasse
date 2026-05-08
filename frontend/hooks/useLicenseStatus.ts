import { useCallback, useEffect, useState } from 'react';

import { apiClient } from '../services/api/config';

/**
 * License snapshot consumed by the POS license-expiry banner.
 * Source: backend's anonymous GET /api/health/license endpoint
 * (available to cashiers without admin permissions).
 */
export type LicenseStatus = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  /** ISO 8601 UTC; null when license has no exp claim. */
  expiryDate: string | null;
  machineHash: string;
};

const POLL_MS = 10 * 60 * 1000; // 10 dakika

/**
 * Lisans durumunu /api/health/license üzerinden alır ve 10 dakikada bir tazeler.
 * Hata/erişim sorunlarında banner sessiz kalır (status === null).
 */
export function useLicenseStatus() {
  const [status, setStatus] = useState<LicenseStatus | null>(null);
  const [loading, setLoading] = useState<boolean>(false);

  const fetchStatus = useCallback(async () => {
    setLoading(true);
    try {
      // apiClient baseURL zaten `/api` ile biter → effective: GET /api/health/license
      const raw = await apiClient.get<Record<string, unknown>>('/health/license');
      setStatus(normalize(raw));
    } catch {
      // Banner non-critical: hata durumunda gizlenmesi yeterli, retry interval bir sonraki tick'te.
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
    return () => clearInterval(id);
  }, [fetchStatus]);

  return { status, loading, refetch: fetchStatus };
}

function normalize(raw: Record<string, unknown> | null | undefined): LicenseStatus | null {
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
  };
}
