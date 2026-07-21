import { useCallback, useState } from 'react';

import {
  fetchPublicCustomerDashboard,
  type PublicCustomerDashboard,
} from '../services/api/publicCustomerDashboardService';

const MIN_PHONE_DIGITS = 6;

function digitCount(value: string): number {
  let n = 0;
  for (const ch of value) {
    if (ch >= '0' && ch <= '9') n += 1;
  }
  return n;
}

export function useCustomerDashboard() {
  const [dashboard, setDashboard] = useState<PublicCustomerDashboard | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  const loadDashboard = useCallback(async (tenant: string, phone: string) => {
    if (!tenant.trim() || !phone.trim()) {
      setError('missing_params');
      setDashboard(null);
      return null;
    }
    if (digitCount(phone) < MIN_PHONE_DIGITS) {
      setError('phone_too_short');
      setDashboard(null);
      return null;
    }

    setIsLoading(true);
    setError(null);
    setNotFound(false);
    try {
      const result = await fetchPublicCustomerDashboard({ tenant, phone });
      setDashboard(result);
      return result;
    } catch (err: unknown) {
      setDashboard(null);
      const status =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { status?: number } }).response?.status
          : undefined;
      if (status === 404) {
        setNotFound(true);
        setError(null);
      } else {
        setError('fetch_failed');
      }
      return null;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const reset = useCallback(() => {
    setDashboard(null);
    setError(null);
    setNotFound(false);
  }, []);

  return { dashboard, isLoading, error, notFound, loadDashboard, reset };
}
