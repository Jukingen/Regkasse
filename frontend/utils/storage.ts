import type { AsyncStorageStatic } from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

const isWeb = Platform.OS === 'web';

type StorageBackend = Pick<
  AsyncStorageStatic,
  'getItem' | 'setItem' | 'removeItem' | 'multiRemove' | 'clear' | 'getAllKeys'
>;

const noopBackend: StorageBackend = {
  getItem: async () => null,
  setItem: async () => undefined,
  removeItem: async () => undefined,
  multiRemove: async () => undefined,
  clear: async () => undefined,
  getAllKeys: async () => [],
};

let nativeAsyncStorage: StorageBackend = noopBackend;

// Safe require for native modules (avoid bundling issues on web).
if (!isWeb) {
  try {
    const NativeAsyncStorage = require('@react-native-async-storage/async-storage').default as
      AsyncStorageStatic | undefined;
    if (NativeAsyncStorage) {
      nativeAsyncStorage = NativeAsyncStorage;
    }
  } catch (e) {
    console.warn('AsyncStorage require failed:', e);
  }
}

function parseJson<T>(raw: string, key: string): T | null {
  try {
    return JSON.parse(raw) as T;
  } catch (e) {
    console.warn(`[storage] JSON.parse failed for key "${key}"`, e);
    return null;
  }
}

/**
 * Universal storage for non-sensitive app data.
 * Native: AsyncStorage. Web: localStorage (Promise API for a consistent surface).
 *
 * Sensitive values (tokens, user profile, tenant bootstrap) must use
 * {@link import('../services/secureStorage').secureStorage} instead.
 */
export const storage = {
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
    return await nativeAsyncStorage.getItem(key);
  },

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
    await nativeAsyncStorage.setItem(key, value);
  },

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
    await nativeAsyncStorage.removeItem(key);
  },

  multiRemove: async (keys: string[]): Promise<void> => {
    if (isWeb) {
      try {
        if (typeof window !== 'undefined' && window.localStorage) {
          keys.forEach((key) => {
            window.localStorage.removeItem(key);
          });
        }
      } catch (e) {
        console.warn('localStorage multiRemove failed', e);
      }
      return;
    }
    await nativeAsyncStorage.multiRemove(keys);
  },

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
    await nativeAsyncStorage.clear();
  },

  /**
   * Read and JSON.parse a value. Returns null when missing or invalid JSON.
   */
  getJson: async <T>(key: string): Promise<T | null> => {
    const raw = await storage.getItem(key);
    if (raw == null) return null;
    return parseJson<T>(raw, key);
  },

  /**
   * JSON.stringify and persist. Throws if value cannot be serialized.
   */
  setJson: async (key: string, value: unknown): Promise<void> => {
    await storage.setItem(key, JSON.stringify(value));
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
            if (key && partials.some((p) => key.includes(p))) {
              keysToRemove.push(key);
            }
          }
          keysToRemove.forEach((k) => {
            window.localStorage.removeItem(k);
          });
        }
      } catch (e) {
        console.warn('localStorage clearByPartialKey failed', e);
      }
    } else {
      try {
        const allKeys = await nativeAsyncStorage.getAllKeys();
        const keysToRemove = allKeys.filter((key) => partials.some((p) => key.includes(p)));
        if (keysToRemove.length > 0) {
          await nativeAsyncStorage.multiRemove(keysToRemove);
        }
      } catch (e) {
        console.warn('AsyncStorage clearByPartialKey failed', e);
      }
    }
  },
};

export default storage;
