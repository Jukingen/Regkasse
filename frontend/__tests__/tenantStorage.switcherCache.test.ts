import { tenantStorage, TENANT_STORAGE_KEYS } from '@/services/tenant/tenantStorage';
import { storage } from '@/utils/storage';

jest.mock('@/utils/storage', () => ({
  storage: {
    getItem: jest.fn(),
    setItem: jest.fn(),
    removeItem: jest.fn(),
  },
}));

describe('tenantStorage switcher cache', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('round-trips switcher list JSON', async () => {
    const rows = [{ id: '1', name: 'Cafe', slug: 'dev', status: 'active', isActive: true }];
    (storage.getItem as jest.Mock).mockResolvedValue(JSON.stringify(rows));

    await expect(tenantStorage.getCachedSwitcherList()).resolves.toEqual(rows);
    expect(storage.getItem).toHaveBeenCalledWith(TENANT_STORAGE_KEYS.switcherList);
  });

  it('returns empty array for invalid cache payload', async () => {
    (storage.getItem as jest.Mock).mockResolvedValue('not-json');

    await expect(tenantStorage.getCachedSwitcherList()).resolves.toEqual([]);
  });

  it('writes switcher list to storage', async () => {
    const rows = [{ id: '1', name: 'Cafe', slug: 'dev', status: 'active', isActive: true }];
    await tenantStorage.setCachedSwitcherList(rows);

    expect(storage.setItem).toHaveBeenCalledWith(
      TENANT_STORAGE_KEYS.switcherList,
      JSON.stringify(rows),
    );
  });
});
