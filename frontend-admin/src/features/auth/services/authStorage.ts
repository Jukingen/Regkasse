/**
 * Service to manage JWT token storage across the application.
 */

const TOKEN_KEY = 'rk_admin_access_token';

export const authStorage = {
    /**
     * Retrieves the stored JWT token.
     */
    getToken: (): string | null => {
        if (typeof window !== 'undefined') {
            return localStorage.getItem(TOKEN_KEY);
        }
        return null;
    },

    /**
     * Stores the JWT token.
     */
    setToken: (token: string): void => {
        if (typeof window !== 'undefined') {
            localStorage.setItem(TOKEN_KEY, token);
        }
    },

    /**
     * Removes the JWT token from storage.
     */
    removeToken: (): void => {
        if (typeof window !== 'undefined') {
            localStorage.removeItem(TOKEN_KEY);
        }
    },

    /**
     * Checks if a token is present in storage.
     */
    hasToken: (): boolean => {
        return !!authStorage.getToken();
    }
};
