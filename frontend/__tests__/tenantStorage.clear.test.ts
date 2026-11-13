import { describe, expect, it, beforeEach, jest } from '@jest/globals';

import { secureStorage } from '@/services/secureStorage';
import { tenantStorage, TENANT_STORAGE_KEYS } from '@/services/tenant/tenantStorage';
import { storage } from '@/utils/storage';

jest.mock('@/services/secureStorage', () => ({
  secureStorage: {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    multiRemove: jest.fn(),
  },
}));

jest.mock('@/utils/storage', () => ({
  storage: {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
    multiRemove: jest.fn(),
  },
}));

describe('tenantStorage.clear', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('removes bootstrap keys including license_bootstrap from secure storage', async () => {
    await tenantStorage.clear();

    expect(secureStorage.multiRemove).toHaveBeenCalledWith([
      TENANT_STORAGE_KEYS.tenantId,
      TENANT_STORAGE_KEYS.tenantSlug,
      TENANT_STORAGE_KEYS.apiBaseUrl,
      TENANT_STORAGE_KEYS.licenseBootstrap,
    ]);
  });

  it('returns null for getTenantSlug when unset', async () => {
    jest.mocked(secureStorage.getItem).mockResolvedValue(null);

    await expect(tenantStorage.getTenantSlug()).resolves.toBeNull();
    expect(secureStorage.getItem).toHaveBeenCalledWith(TENANT_STORAGE_KEYS.tenantSlug);
  });

  it('keeps switcher list cache on AsyncStorage', async () => {
    jest.mocked(storage.getItem).mockResolvedValue(null);
    await tenantStorage.getCachedSwitcherList();
    expect(storage.getItem).toHaveBeenCalledWith(TENANT_STORAGE_KEYS.switcherList);
  });
});
