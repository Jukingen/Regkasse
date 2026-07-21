import { useCallback, useEffect, useState } from 'react';

import { getCompanySettings, type PosCompanyInfo } from '../services/api/companyService';

const COMPANY_POLL_MS = 5 * 60_000;

export type UseCompanySettingsResult = {
  data: PosCompanyInfo | null;
  loading: boolean;
  error: Error | null;
  refresh: () => Promise<void>;
};

/**
 * Tenant company settings for POS (RKSV header + working hours).
 * Source: <c>GET /api/pos/company</c>.
 */
export function useCompanySettings(): UseCompanySettingsResult {
  const [data, setData] = useState<PosCompanyInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const next = await getCompanySettings();
      setData(next);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to load company settings'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    const interval = setInterval(() => {
      void refresh();
    }, COMPANY_POLL_MS);
    return () => {
      clearInterval(interval);
    };
  }, [refresh]);

  return { data, loading, error, refresh };
}
