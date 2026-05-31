import { useCallback, useEffect, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';

import { licenseApi, type TenantLicenseStatusDto } from '../api/license';
import { tenantStorage } from '../services/tenant/tenantStorage';
import { TENANT_WARNING_DAYS_BEFORE_EXPIRY } from '../constants/licenseGracePeriod';

export type MandantLicenseWarningState = {
  daysRemaining: number;
  gracePeriodRemaining: number;
  isInGracePeriod: boolean;
  canAccess: boolean;
  statusMessage?: string | null;
};

const POLL_MS = 10 * 60 * 1000;

/**
 * Polls GET /api/license/status?tenantId=… for mandant grace / pre-expiry warnings (POS banner).
 */
export function useMandantLicenseWarning() {
  const [state, setState] = useState<MandantLicenseWarningState | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const tenantId = await tenantStorage.getTenantId();
      if (!tenantId) {
        setState(null);
        return;
      }

      const data: TenantLicenseStatusDto = await licenseApi.getTenantLicenseStatus(tenantId);
      const daysRemaining =
        typeof data.daysRemaining === 'number' && Number.isFinite(data.daysRemaining)
          ? Math.max(0, Math.floor(data.daysRemaining))
          : 0;
      const gracePeriodRemaining =
        typeof data.gracePeriodRemaining === 'number' && Number.isFinite(data.gracePeriodRemaining)
          ? Math.max(0, Math.floor(data.gracePeriodRemaining))
          : 0;

      setState({
        daysRemaining,
        gracePeriodRemaining,
        isInGracePeriod: data.isInGracePeriod === true,
        canAccess: data.canAccess !== false,
        statusMessage: data.statusMessage ?? null,
      });
    } catch {
      setState(null);
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

  const shouldShowGrace = state?.isInGracePeriod === true && state.gracePeriodRemaining >= 0;
  const shouldShowPreExpiry =
    state != null
    && !state.isInGracePeriod
    && state.daysRemaining > 0
    && state.daysRemaining <= TENANT_WARNING_DAYS_BEFORE_EXPIRY;

  return {
    state,
    shouldShowGrace,
    shouldShowPreExpiry,
    refetch: fetchStatus,
  };
}
