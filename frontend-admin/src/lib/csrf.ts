/**
 * Double-submit CSRF helper for FA → API.
 * Cookie mirror via {@link cookieService}; token must be issued by GET /api/csrf/token.
 */

import { cookieService } from '@/services/cookieService';

export const CSRF_HEADER = 'X-XSRF-TOKEN';
export const CSRF_COOKIE_NAME = 'XSRF-TOKEN';
export const CSRF_COOKIE_MIRROR_HEADER = 'X-CSRF-COOKIE';

type CsrfTokenPayload = {
  token?: string;
  headerName?: string;
  cookieName?: string;
  expiresInHours?: number;
};

type CachedCsrf = {
  token: string;
  headerName: string;
  cookieName: string;
  expiresAtMs: number;
};

let cache: CachedCsrf | null = null;
let inflight: Promise<CachedCsrf> | null = null;

function isMutatingMethod(method: string | undefined): boolean {
  const m = (method ?? 'get').toUpperCase();
  return m === 'POST' || m === 'PUT' || m === 'PATCH' || m === 'DELETE';
}

export function requestNeedsCsrf(method: string | undefined, url: string | undefined): boolean {
  if (!isMutatingMethod(method)) return false;
  const path = (url ?? '').split('?')[0]?.toLowerCase() ?? '';
  if (path.includes('/api/csrf/token')) return false;
  if (path.includes('/api/webhooks/')) return false;
  // Login/refresh are CSRF-exempt on the API.
  if (path.includes('/api/auth/login') || path.includes('/api/auth/refresh')) return false;
  return true;
}

/** Read XSRF-TOKEN from document.cookie via cookieService. */
export function getCsrfTokenFromCookie(): string | null {
  return cookieService.getCsrfToken();
}

export async function ensureCsrfToken(baseURL: string): Promise<CachedCsrf> {
  const now = Date.now();
  if (cache && cache.expiresAtMs > now + 60_000) {
    cookieService.setCsrfToken(cache.token);
    return cache;
  }
  if (inflight) {
    return inflight;
  }

  inflight = (async () => {
    const root = baseURL.replace(/\/$/, '');
    const res = await fetch(`${root}/api/csrf/token`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
      cache: 'no-store',
    });
    if (!res.ok) {
      throw new Error(`CSRF token request failed (${res.status})`);
    }
    const data = (await res.json()) as CsrfTokenPayload;
    const token = typeof data.token === 'string' ? data.token.trim() : '';
    if (!token) {
      throw new Error('CSRF token response missing token');
    }
    const hours =
      typeof data.expiresInHours === 'number' && data.expiresInHours > 0
        ? data.expiresInHours
        : 24;

    // Mirror server-issued token into FA document.cookie for the axios interceptor.
    cookieService.setCsrfToken(token);

    cache = {
      token,
      headerName: data.headerName?.trim() || CSRF_HEADER,
      cookieName: data.cookieName?.trim() || CSRF_COOKIE_NAME,
      expiresAtMs: Date.now() + hours * 3_600_000,
    };
    return cache;
  })().finally(() => {
    inflight = null;
  });

  return inflight;
}

export function clearCsrfTokenCache(): void {
  cache = null;
  cookieService.removeCsrfToken();
}

export function isCsrfForbiddenMessage(message: string | null | undefined): boolean {
  if (!message) return false;
  return message.toUpperCase().includes('CSRF');
}

/** Apply CSRF headers onto a mutable headers bag (axios / fetch). */
export function applyCsrfHeaders(
  headers: Record<string, string>,
  csrf: CachedCsrf,
): Record<string, string> {
  headers[csrf.headerName] = csrf.token;
  headers[CSRF_COOKIE_MIRROR_HEADER] = csrf.token;
  return headers;
}
