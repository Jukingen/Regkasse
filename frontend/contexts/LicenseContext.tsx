import { LicenseStatusProvider, useLicenseStatus, type LicenseStatus } from './LicenseStatusContext';

export type { LicenseStatus };

/** Alias for `LicenseStatusProvider` (cached deployment license snapshot). */
export const LicenseProvider = LicenseStatusProvider;

/**
 * Cached deployment license hook (`GET /api/license/status` + `/api/health/license`).
 * Background refresh at most once per hour; call `refreshLicense()` to bypass cache.
 */
export function useLicense() {
  const { status, loading, refetch } = useLicenseStatus();
  return {
    license: status,
    loading,
    refreshLicense: () => refetch(true),
  };
}
