import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { FORMAT_STORAGE_KEY } from '@/i18n/languageStorage';
import { decodeJwtPayload, isTruthyJwtClaim } from '@/lib/auth/jwtPayload';
import { clearStoredPersonalization } from '@/lib/personalization/storage';

/**
 * FA session presence for Edge `proxy.ts` and `/me` bootstrap.
 *
 * Access and refresh JWTs are HttpOnly cookies set by the API
 * (`rk_admin_access_token` / `rk_admin_refresh_token`). They are not stored in
 * localStorage and are not readable here. POS uses `rk_pos_*` (or SecureStore)
 * so the same browser can hold both sessions. A compact non-secret cookie lets
 * Next.js gate protected routes without the JWT.
 */

/** Dispatched on `removeToken()` so client state (e.g. React Query auth) can resync. */
export const AUTH_SESSION_CLEARED_EVENT = 'rk-admin-auth-cleared';

const LEGACY_ACCESS_TOKEN_KEY = 'rk_admin_access_token';
const LEGACY_REFRESH_TOKEN_KEY = 'rk_admin_refresh_token';
const ACCESS_EXPIRES_AT_KEY = 'rk_admin_access_expires_at';
const IMPERSONATING_KEY = 'rk_admin_impersonating';

/**
 * Backend HttpOnly admin JWT cookie name. Must stay identical to `ACCESS_TOKEN_COOKIE` in `src/proxy.ts`.
 */
export const ACCESS_TOKEN_COOKIE_NAME = 'rk_admin_access_token';

/** Legacy shared API cookie — still cleared/accepted during rollout. */
export const API_ACCESS_TOKEN_COOKIE_NAME = 'access_token';

/**
 * Compact presence cookie for Edge `proxy.ts`. Value is not a secret — AuthGate still validates via /me.
 */
export const EDGE_SESSION_COOKIE_NAME = 'rk_admin_edge_session';

/** Kept for oversized-JWT tests / JwtCookieBudget alignment; FA no longer writes the JWT cookie. */
export const MAX_ACCESS_TOKEN_COOKIE_CHARS = 3500;

export type AuthTokens = {
  accessToken: string;
  refreshToken?: string | null;
  expiresAt?: string | Date | null;
  impersonating?: boolean;
};

function cookieSecureSuffix(): string {
  return typeof window !== 'undefined' && window.location.protocol === 'https:' ? '; Secure' : '';
}

function writeEdgeSessionCookie(): void {
  if (typeof document === 'undefined') {
    return;
  }
  const maxAgeSec = 60 * 60 * 24 * 7;
  document.cookie = `${EDGE_SESSION_COOKIE_NAME}=1; Path=/; SameSite=Lax; Max-Age=${maxAgeSec}${cookieSecureSuffix()}`;
}

function clearClientAuthCookies(): void {
  if (typeof document === 'undefined') {
    return;
  }
  const secure = cookieSecureSuffix();
  document.cookie = `${ACCESS_TOKEN_COOKIE_NAME}=; Path=/; SameSite=Lax; Max-Age=0${secure}`;
  document.cookie = `${API_ACCESS_TOKEN_COOKIE_NAME}=; Path=/; SameSite=Lax; Max-Age=0${secure}`;
  document.cookie = `${EDGE_SESSION_COOKIE_NAME}=; Path=/; SameSite=Lax; Max-Age=0${secure}`;
}

function hasEdgeSessionCookie(): boolean {
  if (typeof document === 'undefined') {
    return false;
  }
  const parts = document.cookie.split(';');
  for (const part of parts) {
    if (part.trim() === `${EDGE_SESSION_COOKIE_NAME}=1`) {
      return true;
    }
  }
  return false;
}

function persistExpiresAt(value: string | Date | number | null | undefined): void {
  if (typeof window === 'undefined') {
    return;
  }
  if (value == null) {
    return;
  }
  let iso: string | null = null;
  if (typeof value === 'number' && Number.isFinite(value)) {
    iso = new Date(value > 1e12 ? value : value * 1000).toISOString();
  } else if (value instanceof Date && !Number.isNaN(value.getTime())) {
    iso = value.toISOString();
  } else if (typeof value === 'string' && value.trim()) {
    const parsed = Date.parse(value);
    if (!Number.isNaN(parsed)) {
      iso = new Date(parsed).toISOString();
    }
  }
  if (iso) {
    window.localStorage.setItem(ACCESS_EXPIRES_AT_KEY, iso);
  }
}

function persistMetadataFromAccessToken(jwt: string): void {
  const payload = decodeJwtPayload(jwt);
  if (!payload) {
    return;
  }
  if (typeof payload.exp === 'number' && Number.isFinite(payload.exp)) {
    persistExpiresAt(payload.exp);
  }
  const impersonating = isTruthyJwtClaim(payload.tenant_impersonation);
  if (typeof window !== 'undefined') {
    if (impersonating) {
      window.localStorage.setItem(IMPERSONATING_KEY, '1');
    } else {
      window.localStorage.removeItem(IMPERSONATING_KEY);
    }
  }
}

function clearLegacyTokenStorage(): void {
  if (typeof window === 'undefined') {
    return;
  }
  window.localStorage.removeItem(LEGACY_ACCESS_TOKEN_KEY);
  window.localStorage.removeItem(LEGACY_REFRESH_TOKEN_KEY);
  window.sessionStorage.removeItem(LEGACY_ACCESS_TOKEN_KEY);
  window.sessionStorage.removeItem(LEGACY_REFRESH_TOKEN_KEY);
}

export const authStorage = {
  /**
   * Access JWT is HttpOnly — not available to JavaScript.
   * Kept for call-site compatibility; always returns null.
   */
  getToken: (): string | null => null,

  /**
   * Marks an authenticated session (Edge cookie + optional JWT metadata).
   * Does not persist the JWT (HttpOnly cookie is set by the API).
   */
  setToken: (token: string): void => {
    const cleanToken = token.startsWith('Bearer ') ? token.slice(7) : token;
    const trimmed = cleanToken.trim();
    if (!trimmed) {
      return;
    }
    writeEdgeSessionCookie();
    persistMetadataFromAccessToken(trimmed);
    clearLegacyTokenStorage();
  },

  /**
   * Login/refresh/impersonation: Edge session + non-secret expiry/impersonation flags.
   */
  setTokens: (tokens: AuthTokens): void => {
    authStorage.setToken(tokens.accessToken);
    if (tokens.expiresAt) {
      persistExpiresAt(tokens.expiresAt);
    }
    if (tokens.impersonating === true && typeof window !== 'undefined') {
      window.localStorage.setItem(IMPERSONATING_KEY, '1');
    }
  },

  /** Refresh JWT is HttpOnly — not available to JavaScript. */
  getRefreshToken: (): string | null => null,

  setRefreshToken: (_refreshToken: string): void => {
    // HttpOnly refresh cookie is set by the API.
  },

  /** Edge presence cookie so proxy.ts and /me bootstrap treat the tab as signed in. */
  markSession: (expiresAt?: string | Date | null): void => {
    writeEdgeSessionCookie();
    persistExpiresAt(expiresAt);
    clearLegacyTokenStorage();
  },

  getAccessExpiresAtMs: (): number | null => {
    if (typeof window === 'undefined') {
      return null;
    }
    const raw = window.localStorage.getItem(ACCESS_EXPIRES_AT_KEY);
    if (!raw) {
      return null;
    }
    const ms = Date.parse(raw);
    return Number.isNaN(ms) ? null : ms;
  },

  isImpersonating: (): boolean => {
    if (typeof window === 'undefined') {
      return false;
    }
    return window.localStorage.getItem(IMPERSONATING_KEY) === '1';
  },

  setImpersonating: (value: boolean): void => {
    if (typeof window === 'undefined') {
      return;
    }
    if (value) {
      window.localStorage.setItem(IMPERSONATING_KEY, '1');
    } else {
      window.localStorage.removeItem(IMPERSONATING_KEY);
    }
  },

  /**
   * Clears client session metadata (tenant, theme, preferences).
   * HttpOnly API cookies are cleared by `POST /api/Auth/logout`.
   */
  removeToken: (): void => {
    if (typeof window !== 'undefined') {
      clearLegacyTokenStorage();
      window.localStorage.removeItem(ACCESS_EXPIRES_AT_KEY);
      window.localStorage.removeItem(IMPERSONATING_KEY);
      clearClientAuthCookies();
      tenantStorage.clear();
      clearStoredPersonalization();
      try {
        window.localStorage.removeItem(FORMAT_STORAGE_KEY);
      } catch {
        /* restricted storage */
      }
      window.dispatchEvent(new CustomEvent(AUTH_SESSION_CLEARED_EVENT));
    }
  },

  /**
   * True when the compact Edge session cookie is present (not a substitute for /me).
   */
  hasToken: (): boolean => {
    return hasEdgeSessionCookie();
  },
};
