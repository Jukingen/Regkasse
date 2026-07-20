/**
 * CSRF helper for tenant websites → API (public online orders, etc.).
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

export async function ensureCsrfToken(baseURL: string): Promise<CachedCsrf> {
  const now = Date.now();
  if (cache && cache.expiresAtMs > now + 60_000) {
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
