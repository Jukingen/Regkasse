
import { Platform } from 'react-native';

const isWeb = Platform.OS === 'web';

let AsyncStorage: any = {
    getItem: async () => null,
    setItem: async () => { },
    removeItem: async () => { },
    multiRemove: async () => { },
    clear: async () => { },
    getAllKeys: async () => []
};

// Safe require for native modules
if (Platform.OS !== 'web') {
    try {
        const NativeAsyncStorage = require('@react-native-async-storage/async-storage').default;
        if (NativeAsyncStorage) {
            AsyncStorage = NativeAsyncStorage;
        }
    } catch (e) {
        console.warn('AsyncStorage require failed:', e);
    }
}

/**
 * Universal Storage Helper
 * Uses localStorage on Web for compatibility and AsyncStorage on Native.
 * Always returns Promises to maintain consistent API across platforms.
 */
export const storage = {
    /**
     * Get value from storage
     */
    getItem: async (key: string): Promise<string | null> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    return window.localStorage.getItem(key);
                }
                return null;
            } catch (e) {
                console.warn('localStorage getItem failed', e);
                return null;
            }
        }
        return AsyncStorage.getItem(key);
    },

    /**
     * Set value in storage
     */
    setItem: async (key: string, value: string): Promise<void> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    window.localStorage.setItem(key, value);
                }
            } catch (e) {
                console.warn('localStorage setItem failed', e);
            }
            return;
        }
        return AsyncStorage.setItem(key, value);
    },

    /**
     * Remove value from storage
     */
    removeItem: async (key: string): Promise<void> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    window.localStorage.removeItem(key);
                }
            } catch (e) {
                console.warn('localStorage removeItem failed', e);
            }
            return;
        }
        return AsyncStorage.removeItem(key);
    },

    /**
     * Remove multiple values from storage
     */
    multiRemove: async (keys: string[]): Promise<void> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    keys.forEach(key => window.localStorage.removeItem(key));
                }
            } catch (e) {
                console.warn('localStorage multiRemove failed', e);
            }
            return;
        }
        return AsyncStorage.multiRemove(keys);
    },

    /**
     * Clear all storage
     */
    clear: async (): Promise<void> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    window.localStorage.clear();
                }
            } catch (e) {
                console.warn('localStorage clear failed', e);
            }
            return;
        }
        return AsyncStorage.clear();
    },

    /**
     * Clears keys containing any of the given substrings.
     * Useful for clearing cart or auth related keys safely.
     */
    clearByPartialKey: async (partials: string[]): Promise<void> => {
        if (isWeb) {
            try {
                if (typeof window !== 'undefined' && window.localStorage) {
                    const keysToRemove: string[] = [];
                    for (let i = 0; i < window.localStorage.length; i++) {
                        const key = window.localStorage.key(i);
                        if (key && partials.some(p => key.includes(p))) {
                            keysToRemove.push(key);
                        }
                    }
                    keysToRemove.forEach(k => window.localStorage.removeItem(k));
                }
            } catch (e) {
                console.warn('localStorage clearByPartialKey failed', e);
            }
        } else {
            try {
                const allKeys = await AsyncStorage.getAllKeys();
                const keysToRemove = allKeys.filter(key => partials.some(p => key.includes(p)));
                if (keysToRemove.length > 0) {
                    await AsyncStorage.multiRemove(keysToRemove);
                }
            } catch (e) {
                console.warn('AsyncStorage clearByPartialKey failed', e);
            }
        }
    }
};

export default storage;
