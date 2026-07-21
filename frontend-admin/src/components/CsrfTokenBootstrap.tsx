'use client';

import { useEffect } from 'react';

import { ensureCsrfToken } from '@/lib/csrf';
import { technicalConsole } from '@/shared/dev/technicalConsole';

function resolveApiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (configured) {
    return configured.replace(/\/$/, '');
  }
  if (process.env.NODE_ENV === 'development') {
    return 'http://localhost:5184';
  }
  return '';
}

/**
 * Root-layout bridge: issue CSRF token on first paint and mirror into `XSRF-TOKEN` cookie.
 * Mounted from `app/layout.tsx` (Server Component cannot call hooks directly).
 *
 * Uses `GET /api/csrf/token` (not a bare client UUID) so the value is registered in the API cache.
 */
export function CsrfTokenBootstrap(): null {
  useEffect(() => {
    const baseURL = resolveApiBaseUrl();
    if (!baseURL) {
      return;
    }

    let cancelled = false;
    void (async () => {
      try {
        await ensureCsrfToken(baseURL);
      } catch (error) {
        if (!cancelled && process.env.NODE_ENV === 'development') {
          technicalConsole.warn('[CSRF] bootstrap on layout failed', error);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  return null;
}
