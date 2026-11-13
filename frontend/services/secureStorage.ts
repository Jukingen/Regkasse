import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import { storage } from '../utils/storage';

/**
 * Secure storage for sensitive POS data (tokens, user, tenant, license).
 * Native: expo-secure-store (Keychain / EncryptedSharedPreferences).
 * Web: falls back to {@link storage} (localStorage) — SecureStore is unavailable on web.
 *
 * Values larger than {@link SECURE_CHUNK_SIZE} are split across keys (Android ~2KB limit).
 * On first read, legacy AsyncStorage / localStorage values are migrated into SecureStore.
 */

/** Stay under Android SecureStore ~2048-byte value limit with headroom for encoding. */
const SECURE_CHUNK_SIZE = 1800;
const CHUNK_COUNT_SUFFIX = '__secure_chunks';

function isNativeSecurePlatform(): boolean {
  return Platform.OS === 'ios' || Platform.OS === 'android';
}

function chunkKey(key: string, index: number): string {
  return `${key}__${index}`;
}

function chunkCountKey(key: string): string {
  return `${key}${CHUNK_COUNT_SUFFIX}`;
}

async function nativeGetRaw(key: string): Promise<string | null> {
  return await SecureStore.getItemAsync(key);
}

async function nativeSetRaw(key: string, value: string): Promise<void> {
  await SecureStore.setItemAsync(key, value);
}

async function nativeDeleteRaw(key: string): Promise<void> {
  await SecureStore.deleteItemAsync(key);
}

async function deleteNativeChunks(key: string): Promise<void> {
  const countRaw = await nativeGetRaw(chunkCountKey(key));
  const count = countRaw ? Number.parseInt(countRaw, 10) : 0;
  if (Number.isFinite(count) && count > 0) {
    await Promise.all(Array.from({ length: count }, (_, i) => nativeDeleteRaw(chunkKey(key, i))));
  }
  await nativeDeleteRaw(chunkCountKey(key));
  await nativeDeleteRaw(key);
}

async function getNativeItem(key: string): Promise<string | null> {
  const countRaw = await nativeGetRaw(chunkCountKey(key));
  if (countRaw) {
    const count = Number.parseInt(countRaw, 10);
    if (!Number.isFinite(count) || count <= 0) {
      await deleteNativeChunks(key);
      return null;
    }
    const parts: string[] = [];
    for (let i = 0; i < count; i += 1) {
      const part = await nativeGetRaw(chunkKey(key, i));
      if (part == null) {
        await deleteNativeChunks(key);
        return null;
      }
      parts.push(part);
    }
    return parts.join('');
  }
  return await nativeGetRaw(key);
}

async function setNativeItem(key: string, value: string): Promise<void> {
  await deleteNativeChunks(key);

  if (value.length <= SECURE_CHUNK_SIZE) {
    await nativeSetRaw(key, value);
    return;
  }

  const chunkCount = Math.ceil(value.length / SECURE_CHUNK_SIZE);
  await nativeSetRaw(chunkCountKey(key), String(chunkCount));
  for (let i = 0; i < chunkCount; i += 1) {
    const start = i * SECURE_CHUNK_SIZE;
    await nativeSetRaw(chunkKey(key, i), value.slice(start, start + SECURE_CHUNK_SIZE));
  }
}

/**
 * Migrate a legacy plaintext value from AsyncStorage/localStorage into SecureStore once.
 */
async function migrateFromLegacy(key: string): Promise<string | null> {
  try {
    const legacy = await storage.getItem(key);
    if (legacy == null) return null;

    if (isNativeSecurePlatform()) {
      await setNativeItem(key, legacy);
      await storage.removeItem(key);
    }
    return legacy;
  } catch (error) {
    console.warn(`[secureStorage] legacy migrate failed for ${key}:`, error);
    return null;
  }
}

export const secureStorage = {
  async getItem(key: string): Promise<string | null> {
    if (!isNativeSecurePlatform()) {
      return await storage.getItem(key);
    }

    try {
      const existing = await getNativeItem(key);
      if (existing != null) return existing;
      return await migrateFromLegacy(key);
    } catch (error) {
      console.warn(`[secureStorage] getItem failed for ${key}:`, error);
      return await migrateFromLegacy(key);
    }
  },

  async setItem(key: string, value: string): Promise<void> {
    if (!isNativeSecurePlatform()) {
      await storage.setItem(key, value);
      return;
    }

    try {
      await setNativeItem(key, value);
      // Drop plaintext legacy copy if present
      await storage.removeItem(key);
    } catch (error) {
      console.warn(`[secureStorage] setItem failed for ${key}:`, error);
      throw error;
    }
  },

  async removeItem(key: string): Promise<void> {
    if (!isNativeSecurePlatform()) {
      await storage.removeItem(key);
      return;
    }

    try {
      await deleteNativeChunks(key);
    } catch (error) {
      console.warn(`[secureStorage] removeItem failed for ${key}:`, error);
    }
    await storage.removeItem(key);
  },

  async multiRemove(keys: string[]): Promise<void> {
    await Promise.all(keys.map((key) => this.removeItem(key)));
  },

  async multiGet(keys: string[]): Promise<[string, string | null][]> {
    const values = await Promise.all(keys.map((key) => this.getItem(key)));
    return keys.map((key, index) => [key, values[index]]);
  },

  async multiSet(pairs: [string, string][]): Promise<void> {
    await Promise.all(pairs.map(([key, value]) => this.setItem(key, value)));
  },
};

export default secureStorage;
