import { useEffect, useState } from 'react';

import { licenseApi } from '@/api/license';

export type LicenseStatusReadModel = {
  licenseType: string;
  daysRemaining: number;
  isExpired: boolean;
  isLoading: boolean;
};

const POLL_MS = 60_000;

const DEFAULT_STATE: LicenseStatusReadModel = {
  licenseType: 'Trial',
  daysRemaining: 30,
  isExpired: false,
  isLoading: true,
};

/**
 * Polls GET /api/license/status for POS-facing license mode (trial / licensed / expired).
 * On failure, keeps last known values and clears `isLoading` (non-blocking).
 */
export function useLicenseStatus(): LicenseStatusReadModel {
  const [status, setStatus] = useState<LicenseStatusReadModel>(DEFAULT_STATE);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const response = await licenseApi.getStatus();
        setStatus({
          licenseType: response.licenseType,
          daysRemaining: response.daysRemaining,
          isExpired: response.isExpired,
          isLoading: false,
        });
      } catch {
        // eslint-disable-next-line no-console
        console.warn('Failed to fetch license status, using fallback');
        setStatus((prev) => ({ ...prev, isLoading: false }));
      }
    };

    fetchStatus().then(() => undefined);
    const interval = setInterval(() => {
      fetchStatus().then(() => undefined);
    }, POLL_MS);
    return () => clearInterval(interval);
  }, []);

  return status;
}
