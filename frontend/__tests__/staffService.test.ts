import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import { clearPosStaffCache, listPosStaff } from '../services/api/staffService';

const mockGet = jest.fn<(...args: unknown[]) => Promise<{ data: unknown }>>();

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: (...args: unknown[]) => mockGet(...args),
  },
}));

describe('listPosStaff cache', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    clearPosStaffCache();
    mockGet.mockResolvedValue({
      data: [{ id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' }],
    });
  });

  it('caches GET /api/pos/staff for 5 minutes', async () => {
    const first = await listPosStaff();
    const second = await listPosStaff();

    expect(first).toEqual([{ id: 'staff-1', name: 'Anna Kasse', role: 'Cashier' }]);
    expect(second).toBe(first);
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet).toHaveBeenCalledWith('/pos/staff');
  });
});
