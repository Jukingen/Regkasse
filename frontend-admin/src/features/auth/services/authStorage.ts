import { tenantStorage } from '@/features/auth/services/tenantStorage';

/**
 * Service to manage JWT token storage across the application.
 * Primary persistence is localStorage so new browser tabs share the same admin session
 * (sessionStorage is isolated per tab). Tokens previously stored only in sessionStorage
 * are migrated once on read.
 *
 * Access token is mirrored to a non-HttpOnly cookie so Next.js `proxy.ts` can gate
 * protected routes (Edge cannot read localStorage). Axios uses the in-memory /
 * localStorage value as `Authorization: Bearer …`.
 */

/** Dispatched on `removeToken()` so client state (e.g. React Query auth) can resync. */
export const AUTH_SESSION_CLEARED_EVENT = 'rk-admin-auth-cleared';

const ACCESS_TOKEN_KEY = 'rk_admin_access_token';
const REFRESH_TOKEN_KEY = 'rk_admin_refresh_token';
/**
 * Must stay identical to `ACCESS_TOKEN_COOKIE` in `src/proxy.ts`.
 * Do not rename without updating the Edge proxy.
 */
export const ACCESS_TOKEN_COOKIE_NAME = 'rk_admin_access_token';

let accessTokenMemory: string | null = null;

const normalizeToken = (token: string): string =>
  token.startsWith('Bearer ') ? token.slice(7) : token;

export type AuthTokens = {
  accessToken: string;
  refreshToken?: string | null;
};

function writeAccessTokenCookie(jwt: string): void {
  if (typeof document === 'undefined') {
    return;
  }
  const secure =
    typeof window !== 'undefined' && window.location.protocol === 'https:' ? '; Secure' : '';
  const maxAgeSec = 60 * 60 * 24 * 7;
  // encodeURIComponent keeps JWT safe in Cookie headers; Next.js cookie reader decodes.
  document.cookie = `${ACCESS_TOKEN_COOKIE_NAME}=${encodeURIComponent(jwt)}; Path=/; SameSite=Lax; Max-Age=${maxAgeSec}${secure}`;
}

function clearAccessTokenCookie(): void {
  if (typeof document === 'undefined') {
    return;
  }
  const secure =
    typeof window !== 'undefined' && window.location.protocol === 'https:' ? '; Secure' : '';
  document.cookie = `${ACCESS_TOKEN_COOKIE_NAME}=; Path=/; SameSite=Lax; Max-Age=0${secure}`;
}

/** Reads `rk_admin_access_token` from `document.cookie` (decoded). */
export function readAccessTokenCookie(): string | null {
  if (typeof document === 'undefined') {
    return null;
  }
  const prefix = `${ACCESS_TOKEN_COOKIE_NAME}=`;
  const parts = document.cookie.split(';');
  for (const part of parts) {
    const trimmed = part.trim();
    if (!trimmed.startsWith(prefix)) {
      continue;
    }
    const raw = trimmed.slice(prefix.length);
    if (!raw) {
      return null;
    }
    try {
      return decodeURIComponent(raw);
    } catch {
      return raw;
    }
  }
  return null;
}

function readAccessFromPersistence(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }
  let token = window.localStorage.getItem(ACCESS_TOKEN_KEY);
  if (!token) {
    const legacy = window.sessionStorage.getItem(ACCESS_TOKEN_KEY);
    if (legacy) {
      window.localStorage.setItem(ACCESS_TOKEN_KEY, legacy);
      window.sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      token = legacy;
    }
  }
  return token;
}

function readRefreshFromPersistence(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }
  let refresh = window.localStorage.getItem(REFRESH_TOKEN_KEY);
  if (!refresh) {
    const legacy = window.sessionStorage.getItem(REFRESH_TOKEN_KEY);
    if (legacy) {
      window.localStorage.setItem(REFRESH_TOKEN_KEY, legacy);
      window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      refresh = legacy;
    }
  }
  return refresh;
}

export const authStorage = {
  /**
   * Retrieves the stored JWT token (memory → localStorage → re-mirror cookie).
   */
  getToken: (): string | null => {
    if (accessTokenMemory) {
      return accessTokenMemory;
    }
    const token = readAccessFromPersistence();
    accessTokenMemory = token;
    if (token) {
      writeAccessTokenCookie(token);
    }
    return token;
  },

  /**
   * Stores the JWT access token in memory, localStorage, and the proxy cookie.
   */
  setToken: (token: string): void => {
    const cleanToken = normalizeToken(token).trim();
    if (!cleanToken) {
      return;
    }
    accessTokenMemory = cleanToken;
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(ACCESS_TOKEN_KEY, cleanToken);
      window.sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      writeAccessTokenCookie(cleanToken);
    }
  },

  /**
   * Atomic login/refresh persist: access token (header source + proxy cookie) and optional refresh
   * (localStorage only — never put refresh tokens in a readable cookie).
   */
  setTokens: (tokens: AuthTokens): void => {
    authStorage.setToken(tokens.accessToken);
    if (tokens.refreshToken) {
      authStorage.setRefreshToken(tokens.refreshToken);
    }
  },

  getRefreshToken: (): string | null => {
    return readRefreshFromPersistence();
  },

  setRefreshToken: (refreshToken: string): void => {
    const cleanRefreshToken = refreshToken.trim();
    if (!cleanRefreshToken) {
      return;
    }
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(REFRESH_TOKEN_KEY, cleanRefreshToken);
      window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    }
  },

  /**
   * Removes the JWT token from memory, localStorage, sessionStorage, and the proxy cookie.
   */
  removeToken: (): void => {
    accessTokenMemory = null;
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(ACCESS_TOKEN_KEY);
      window.localStorage.removeItem(REFRESH_TOKEN_KEY);
      window.sessionStorage.removeItem(ACCESS_TOKEN_KEY);
      window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
      clearAccessTokenCookie();
      tenantStorage.clear();
      window.dispatchEvent(new CustomEvent(AUTH_SESSION_CLEARED_EVENT));
    }
  },

  /**
   * Checks if a token is present in storage.
   */
  hasToken: (): boolean => {
    return !!authStorage.getToken();
  },
};
