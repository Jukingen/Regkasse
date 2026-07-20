'use client';

import { useEffect, useState } from 'react';
import { fetchWebsiteStatusLive, type WebsiteStatus } from '@/lib/publicApi';

const POLL_MS = 60_000;

export type UseWebsiteStatusResult = {
  data: WebsiteStatus | null;
  loading: boolean;
  error: boolean;
};

/**
 * Polls GET /api/sites/{slug}/status for customer websites / apps.
 * Never import this into POS or FA — working hours must not gate cashiers.
 */
export function useWebsiteStatus(tenantSlug: string): UseWebsiteStatusResult {
  const [data, setData] = useState<WebsiteStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    const slug = tenantSlug?.trim();
    if (!slug) {
      setData(null);
      setLoading(false);
      setError(true);
      return;
    }

    let cancelled = false;

    const load = async () => {
      const next = await fetchWebsiteStatusLive(slug);
      if (cancelled) return;
      if (!next) {
        setError(true);
        setLoading(false);
        return;
      }
      setData(next);
      setError(false);
      setLoading(false);
    };

    void load();
    const id = window.setInterval(() => {
      void load();
    }, POLL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, [tenantSlug]);

  return { data, loading, error };
}
