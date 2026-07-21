import { describe, expect, it, beforeEach, jest } from '@jest/globals';
import * as SecureStore from 'expo-secure-store';

import { secureStorage } from '../services/secureStorage';
import { storage } from '../utils/storage';

const mockSecureStoreMap = new Map<string, string>();
const mockPlatformState = { OS: 'ios' as string };

jest.mock('react-native', () => ({
  Platform: {
    get OS() {
      return mockPlatformState.OS;
    },
  },
}));

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async (key: string) =>
    mockSecureStoreMap.has(key) ? mockSecureStoreMap.get(key)! : null
  ),
  setItemAsync: jest.fn(async (key: string, value: string) => {
    mockSecureStoreMap.set(key, value);
  }),
  deleteItemAsync: jest.fn(async (key: string) => {
    mockSecureStoreMap.delete(key);
  }),
}));

jest.mock('../utils/storage', () => ({
  storage: {
    getItem: jest.fn(async () => null),
    setItem: jest.fn(async () => undefined),
    removeItem: jest.fn(async () => undefined),
    multiRemove: jest.fn(async () => undefined),
  },
}));

describe('secureStorage', () => {
  beforeEach(async () => {
    mockSecureStoreMap.clear();
    mockPlatformState.OS = 'ios';
    jest.clearAllMocks();
    jest.mocked(storage.getItem).mockResolvedValue(null);
    jest.mocked(storage.removeItem).mockResolvedValue(undefined);
    await secureStorage.multiRemove(['token', 'refreshToken', 'user']);
    mockSecureStoreMap.clear();
    jest.clearAllMocks();
  });

  it('persists and reads auth token via SecureStore on native', async () => {
    await secureStorage.setItem('token', 'access-token-value');
    await expect(secureStorage.getItem('token')).resolves.toBe('access-token-value');
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('token', 'access-token-value');
    expect(storage.removeItem).toHaveBeenCalledWith('token');
  });

  it('migrates legacy AsyncStorage value into SecureStore on first read', async () => {
    jest.mocked(storage.getItem).mockResolvedValueOnce('legacy-refresh');
    await expect(secureStorage.getItem('refreshToken')).resolves.toBe('legacy-refresh');
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('refreshToken', 'legacy-refresh');
    expect(storage.removeItem).toHaveBeenCalledWith('refreshToken');
  });

  it('chunks large values under SecureStore size limit', async () => {
    const large = 'x'.repeat(4000);
    await secureStorage.setItem('user', large);
    await expect(secureStorage.getItem('user')).resolves.toBe(large);
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('user__secure_chunks', '3');
  });

  it('falls back to storage helper on web', async () => {
    mockPlatformState.OS = 'web';
    jest.mocked(storage.getItem).mockResolvedValue('web-token');
    await expect(secureStorage.getItem('token')).resolves.toBe('web-token');
    expect(SecureStore.getItemAsync).not.toHaveBeenCalled();
  });
});
