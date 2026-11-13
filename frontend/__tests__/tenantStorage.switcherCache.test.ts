import { describe, expect, it, beforeEach, jest } from '@jest/globals';

import { sessionManager } from '@/services/session/sessionManager';
import {
  fetchFreshTenants,
  tenantStorage,
  TENANT_STORAGE_KEYS,
} from '@/services/tenant/tenantStorage';
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
  },
}));

jest.mock('@/services/session/sessionManager', () => ({
  sessionManager: {
    getAccessToken: jest.fn(),
    isExpired: jest.fn(),
  },
}));

jest.mock('@/services/tenant/tenantSwitcherApi', () => ({
  fetchTenantSwitcherList: jest.fn(),
}));

describe('tenantStorage switcher cache', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('round-trips switcher list JSON', async () => {
    const rows = [{ id: '1', name: 'Cafe', slug: 'dev', status: 'active', isActive: true }];
    jest.mocked(storage.getItem).mockResolvedValue(JSON.stringify(rows));

    await expect(tenantStorage.getCachedSwitcherList()).resolves.toEqual(rows);
    expect(storage.getItem).toHaveBeenCalledWith(TENANT_STORAGE_KEYS.switcherList);
  });

  it('returns empty array for invalid cache payload', async () => {
    jest.mocked(storage.getItem).mockResolvedValue('not-json');

    await expect(tenantStorage.getCachedSwitcherList()).resolves.toEqual([]);
  });

  it('writes switcher list to storage', async () => {
    const rows = [{ id: '1', name: 'Cafe', slug: 'dev', status: 'active', isActive: true }];
    await tenantStorage.setCachedSwitcherList(rows);

    expect(storage.setItem).toHaveBeenCalledWith(
      TENANT_STORAGE_KEYS.switcherList,
      JSON.stringify(rows)
    );
  });
});

describe('fetchFreshTenants', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.mocked(storage.getItem).mockResolvedValue(null);
  });

  it('skips API when there is no access token', async () => {
    const { fetchTenantSwitcherList } = jest.requireMock('@/services/tenant/tenantSwitcherApi') as {
      fetchTenantSwitcherList: jest.Mock;
    };
    jest.mocked(sessionManager.getAccessToken).mockResolvedValue(null);

    await expect(fetchFreshTenants()).resolves.toEqual({ tenants: [], fromCache: false });
    expect(fetchTenantSwitcherList).not.toHaveBeenCalled();
  });

  it('skips API when access token is expired', async () => {
    const { fetchTenantSwitcherList } = jest.requireMock('@/services/tenant/tenantSwitcherApi') as {
      fetchTenantSwitcherList: jest.Mock;
    };
    jest.mocked(sessionManager.getAccessToken).mockResolvedValue('expired-token');
    jest.mocked(sessionManager.isExpired).mockReturnValue(true);

    await expect(fetchFreshTenants()).resolves.toEqual({ tenants: [], fromCache: false });
    expect(fetchTenantSwitcherList).not.toHaveBeenCalled();
  });
});
