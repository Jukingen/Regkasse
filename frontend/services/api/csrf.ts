/**
 * Double-submit CSRF helper for POS → API.
 * Native clients mirror the token in X-CSRF-COOKIE (no reliable HttpOnly cookie jar).
 */

export const CSRF_HEADER = 'X-XSRF-TOKEN';
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
  if (path.includes('/csrf/token')) return false;
  if (path.includes('/webhooks/')) return false;
  return true;
}

export async function ensureCsrfToken(baseURL: string): Promise<CachedCsrf> {
  const now = Date.now();
  if (cache && cache.expiresAtMs > now + 60_000) {
    return cache;
  }
  if (inflight) {
    return inflight;
  }

  inflight = (async () => {
    const root = baseURL.replace(/\/$/, '').replace(/\/api$/i, '');
    const res = await fetch(`${root}/api/csrf/token`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
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
    cache = {
      token,
      headerName: data.headerName?.trim() || CSRF_HEADER,
      expiresAtMs: Date.now() + hours * 3_600_000,
    };
    return cache;
  })().finally(() => {
    inflight = null;
  });

  return inflight;
}

export function applyCsrfHeaders(
  headers: Record<string, string>,
  csrf: CachedCsrf,
): Record<string, string> {
  headers[csrf.headerName] = csrf.token;
  headers[CSRF_COOKIE_MIRROR_HEADER] = csrf.token;
  return headers;
}
