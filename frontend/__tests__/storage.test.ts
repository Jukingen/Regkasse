import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import { storage } from '../utils/storage';

const mockStore = new Map<string, string>();

jest.mock('react-native', () => ({
  Platform: { OS: 'android' },
}));

jest.mock('@react-native-async-storage/async-storage', () => ({
  __esModule: true,
  default: {
    getItem: jest.fn(async (key: string) => (mockStore.has(key) ? mockStore.get(key)! : null)),
    setItem: jest.fn(async (key: string, value: string) => {
      mockStore.set(key, value);
    }),
    removeItem: jest.fn(async (key: string) => {
      mockStore.delete(key);
    }),
    multiRemove: jest.fn(async (keys: string[]) => {
      keys.forEach((key) => mockStore.delete(key));
    }),
    clear: jest.fn(async () => {
      mockStore.clear();
    }),
    getAllKeys: jest.fn(async () => Array.from(mockStore.keys())),
  },
}));

describe('storage JSON helpers', () => {
  beforeEach(() => {
    mockStore.clear();
    jest.clearAllMocks();
  });

  it('round-trips objects via setJson/getJson', async () => {
    await storage.setJson('cart-storage', { state: { activeTableId: 2 }, version: 0 });
    await expect(
      storage.getJson<{ state: { activeTableId: number } }>('cart-storage')
    ).resolves.toEqual({
      state: { activeTableId: 2 },
      version: 0,
    });
  });

  it('returns null for missing keys', async () => {
    await expect(storage.getJson('missing')).resolves.toBeNull();
  });

  it('returns null for invalid JSON instead of throwing', async () => {
    await storage.setItem('broken', '{not-json');
    await expect(storage.getJson('broken')).resolves.toBeNull();
  });
});
