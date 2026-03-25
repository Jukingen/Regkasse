/**
 * Service to manage JWT token storage across the application.
 */

const ACCESS_TOKEN_KEY = 'rk_admin_access_token';
const REFRESH_TOKEN_KEY = 'rk_admin_refresh_token';
let accessTokenMemory: string | null = null;

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
        accessTokenMemory = token;
        if (typeof window !== 'undefined') {
            sessionStorage.setItem(ACCESS_TOKEN_KEY, token);
        }
    },

    getRefreshToken: (): string | null => {
        if (typeof window !== 'undefined') {
            return sessionStorage.getItem(REFRESH_TOKEN_KEY);
        }
        return null;
    },

    setRefreshToken: (refreshToken: string): void => {
        if (typeof window !== 'undefined') {
            sessionStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
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
        }
    },

    /**
     * Checks if a token is present in storage.
     */
    hasToken: (): boolean => {
        return !!authStorage.getToken();
    }
};
