import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const setCsrfToken = vi.fn();
const removeCsrfToken = vi.fn();
const getCsrfToken = vi.fn(() => null);

vi.mock('@/services/cookieService', () => ({
  cookieService: {
    setCsrfToken: (...args: unknown[]) => setCsrfToken(...args),
    removeCsrfToken: (...args: unknown[]) => removeCsrfToken(...args),
    getCsrfToken: (...args: unknown[]) => getCsrfToken(...args),
  },
}));

describe('csrf helpers', () => {
  beforeEach(() => {
    vi.resetModules();
    setCsrfToken.mockReset();
    removeCsrfToken.mockReset();
    getCsrfToken.mockReset();
    getCsrfToken.mockReturnValue(null);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('requestNeedsCsrf only for mutating non-exempt paths', async () => {
    const { requestNeedsCsrf } = await import('@/lib/csrf');
    expect(requestNeedsCsrf('GET', '/api/admin/users')).toBe(false);
    expect(requestNeedsCsrf('POST', '/api/csrf/token')).toBe(false);
    expect(requestNeedsCsrf('POST', '/api/webhooks/stripe')).toBe(false);
    expect(requestNeedsCsrf('POST', '/api/Auth/login')).toBe(false);
    expect(requestNeedsCsrf('POST', '/api/Auth/refresh')).toBe(false);
    expect(requestNeedsCsrf('PUT', '/api/admin/users/1')).toBe(true);
    expect(requestNeedsCsrf('DELETE', '/api/admin/products/1?x=1')).toBe(true);
  });

  it('isCsrfForbiddenMessage detects CSRF text case-insensitively', async () => {
    const { isCsrfForbiddenMessage } = await import('@/lib/csrf');
    expect(isCsrfForbiddenMessage(null)).toBe(false);
    expect(isCsrfForbiddenMessage('')).toBe(false);
    expect(isCsrfForbiddenMessage('csrf token missing')).toBe(true);
    expect(isCsrfForbiddenMessage('CSRF validation failed')).toBe(true);
    expect(isCsrfForbiddenMessage('Unauthorized')).toBe(false);
  });

  it('applyCsrfHeaders sets header and cookie mirror', async () => {
    const { applyCsrfHeaders, CSRF_COOKIE_MIRROR_HEADER, CSRF_HEADER } = await import('@/lib/csrf');
    const headers: Record<string, string> = {};
    applyCsrfHeaders(headers, {
      token: 'tok',
      headerName: CSRF_HEADER,
      cookieName: 'XSRF-TOKEN',
      expiresAtMs: Date.now() + 1000,
    });
    expect(headers[CSRF_HEADER]).toBe('tok');
    expect(headers[CSRF_COOKIE_MIRROR_HEADER]).toBe('tok');
  });

  it('ensureCsrfToken fetches, caches, and reuses token', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        token: 'abc123',
        headerName: 'X-XSRF-TOKEN',
        cookieName: 'XSRF-TOKEN',
        expiresInHours: 1,
      }),
    });
    vi.stubGlobal('fetch', fetchMock);

    const { ensureCsrfToken, clearCsrfTokenCache, getCsrfTokenFromCookie } =
      await import('@/lib/csrf');
    clearCsrfTokenCache();

    const first = await ensureCsrfToken('http://localhost:5184/');
    expect(first.token).toBe('abc123');
    expect(setCsrfToken).toHaveBeenCalledWith('abc123');
    expect(fetchMock).toHaveBeenCalledTimes(1);

    const second = await ensureCsrfToken('http://localhost:5184');
    expect(second.token).toBe('abc123');
    expect(fetchMock).toHaveBeenCalledTimes(1);

    getCsrfToken.mockReturnValue('abc123');
    expect(getCsrfTokenFromCookie()).toBe('abc123');
  });

  it('ensureCsrfToken throws on HTTP or missing token', async () => {
    const { ensureCsrfToken, clearCsrfTokenCache } = await import('@/lib/csrf');
    clearCsrfTokenCache();

    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: false, status: 500, json: async () => ({}) })
    );
    await expect(ensureCsrfToken('http://localhost:5184')).rejects.toThrow(
      /CSRF token request failed/
    );

    clearCsrfTokenCache();
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, json: async () => ({ token: '  ' }) })
    );
    await expect(ensureCsrfToken('http://localhost:5184')).rejects.toThrow(/missing token/);
  });

  it('clearCsrfTokenCache clears cookie mirror', async () => {
    const { clearCsrfTokenCache } = await import('@/lib/csrf');
    clearCsrfTokenCache();
    expect(removeCsrfToken).toHaveBeenCalled();
  });
});
