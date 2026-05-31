import { tenantStorage, TENANT_STORAGE_KEYS } from '@/services/tenant/tenantStorage';
import { storage } from '@/utils/storage';

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

  it('removes bootstrap keys including license_bootstrap', async () => {
    await tenantStorage.clear();

    expect(storage.multiRemove).toHaveBeenCalledWith([
      TENANT_STORAGE_KEYS.tenantId,
      TENANT_STORAGE_KEYS.tenantSlug,
      TENANT_STORAGE_KEYS.apiBaseUrl,
      TENANT_STORAGE_KEYS.licenseBootstrap,
    ]);
  });

  it('returns null for getTenantSlug when unset', async () => {
    (storage.getItem as jest.Mock).mockResolvedValue(null);

    await expect(tenantStorage.getTenantSlug()).resolves.toBeNull();
    expect(storage.getItem).toHaveBeenCalledWith(TENANT_STORAGE_KEYS.tenantSlug);
  });
});
