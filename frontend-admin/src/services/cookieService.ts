/**
 * Browser cookie helpers for FA CSRF double-submit (`XSRF-TOKEN`).
 *
 * Prefer {@link cookieService.setCsrfToken} with the token from `GET /api/csrf/token`
 * so the value is registered in the API cache. A bare client UUID will not pass
 * server-side CSRF validation.
 */
export const cookieService = {
  /**
   * Writes `XSRF-TOKEN` to `document.cookie`.
   * Pass the server-issued token when available; otherwise generates a local UUID
   * (useful for same-origin UX only — not sufficient alone for API CSRF).
   */
  setCsrfToken: (token?: string): string => {
    if (typeof document === 'undefined') {
      return token ?? '';
    }
    const value = token ?? crypto.randomUUID();
    // SameSite=Strict keeps the cookie on the FA origin; axios also mirrors via X-CSRF-COOKIE.
    document.cookie = `XSRF-TOKEN=${encodeURIComponent(value)}; path=/; SameSite=Strict`;
    return value;
  },

  getCsrfToken: (): string | null => {
    if (typeof document === 'undefined') {
      return null;
    }
    const match = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]+)/);
    if (!match?.[1]) {
      return null;
    }
    try {
      return decodeURIComponent(match[1]);
    } catch {
      return match[1];
    }
  },

  removeCsrfToken: (): void => {
    if (typeof document === 'undefined') {
      return;
    }
    document.cookie = 'XSRF-TOKEN=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT';
  },
};
