import { apiClient } from '@/services/api/config';
import { fetchTenantSwitcherList } from '@/services/tenant/tenantSwitcherApi';

jest.mock('@/services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
  },
}));

describe('fetchTenantSwitcherList', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('calls GET /api/tenants/switcher without deleted tenants by default', async () => {
    const rows = [{ id: '1', name: 'Cafe', slug: 'dev', status: 'active', isActive: true }];
    (apiClient.get as jest.Mock).mockResolvedValue(rows);

    await expect(fetchTenantSwitcherList()).resolves.toEqual(rows);
    expect(apiClient.get).toHaveBeenCalledWith('/tenants/switcher', {
      params: { includeDeleted: false },
    });
  });
});
