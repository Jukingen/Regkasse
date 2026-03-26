/**
 * Service to manage JWT token storage across the application.
 */

const ACCESS_TOKEN_KEY = 'rk_admin_access_token';
const REFRESH_TOKEN_KEY = 'rk_admin_refresh_token';
let accessTokenMemory: string | null = null;
const normalizeToken = (token: string): string => token.startsWith('Bearer ') ? token.slice(7) : token;

export const authStorage = {
    /**
     * Retrieves the stored JWT token.
     */
    getToken: (): string | null => {
        if (accessTokenMemory) {
            return accessTokenMemory;
        }
        if (typeof window !== 'undefined') {
            const token = sessionStorage.getItem(ACCESS_TOKEN_KEY);
            accessTokenMemory = token;
            return token;
        }
        return null;
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
            sessionStorage.setItem(ACCESS_TOKEN_KEY, cleanToken);
        }
    },

    getRefreshToken: (): string | null => {
        if (typeof window !== 'undefined') {
            return sessionStorage.getItem(REFRESH_TOKEN_KEY);
        }
        return null;
    },

    setRefreshToken: (refreshToken: string): void => {
        const cleanRefreshToken = refreshToken.trim();
        if (!cleanRefreshToken) {
            return;
        }
        if (typeof window !== 'undefined') {
            sessionStorage.setItem(REFRESH_TOKEN_KEY, cleanRefreshToken);
        }
    },

    /**
     * Removes the JWT token from storage.
     */
    removeToken: (): void => {
        accessTokenMemory = null;
        if (typeof window !== 'undefined') {
            sessionStorage.removeItem(ACCESS_TOKEN_KEY);
            sessionStorage.removeItem(REFRESH_TOKEN_KEY);
            // Legacy cleanup: make sure old localStorage keys do not survive.
            window.localStorage?.removeItem(ACCESS_TOKEN_KEY);
            window.localStorage?.removeItem(REFRESH_TOKEN_KEY);
        }
    },

    /**
     * Checks if a token is present in storage.
     */
    hasToken: (): boolean => {
        return !!authStorage.getToken();
    }
};
