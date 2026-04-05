/**
 * Service to manage JWT token storage across the application.
 * Primary persistence is localStorage so new browser tabs share the same admin session
 * (sessionStorage is isolated per tab). Tokens previously stored only in sessionStorage
 * are migrated once on read.
 */

/** Dispatched on `removeToken()` so client state (e.g. React Query auth) can resync. */
export const AUTH_SESSION_CLEARED_EVENT = 'rk-admin-auth-cleared';

const ACCESS_TOKEN_KEY = 'rk_admin_access_token';
const REFRESH_TOKEN_KEY = 'rk_admin_refresh_token';
let accessTokenMemory: string | null = null;
const normalizeToken = (token: string): string => token.startsWith('Bearer ') ? token.slice(7) : token;

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
     * Retrieves the stored JWT token.
     */
    getToken: (): string | null => {
        if (accessTokenMemory) {
            return accessTokenMemory;
        }
        const token = readAccessFromPersistence();
        accessTokenMemory = token;
        return token;
    },

    /**
     * Stores the JWT token.
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
     * Removes the JWT token from storage.
     */
    removeToken: (): void => {
        accessTokenMemory = null;
        if (typeof window !== 'undefined') {
            window.localStorage.removeItem(ACCESS_TOKEN_KEY);
            window.localStorage.removeItem(REFRESH_TOKEN_KEY);
            window.sessionStorage.removeItem(ACCESS_TOKEN_KEY);
            window.sessionStorage.removeItem(REFRESH_TOKEN_KEY);
            window.dispatchEvent(new CustomEvent(AUTH_SESSION_CLEARED_EVENT));
        }
    },

    /**
     * Checks if a token is present in storage.
     */
    hasToken: (): boolean => {
        return !!authStorage.getToken();
    }
};
