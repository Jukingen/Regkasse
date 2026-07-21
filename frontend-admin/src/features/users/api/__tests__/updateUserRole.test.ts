/**
 * updateUserRole – PUT body assigns tenant user role.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { updateUserRole } from '../users';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: { url: string; method: string; data?: unknown }) =>
    mockCustomInstance(config),
}));

describe('updateUserRole', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls PUT with role only', async () => {
    const result = {
      userId: 'u1',
      userName: 'jane',
      email: 'jane@test.com',
      name: 'Jane Doe',
      role: 'CustomRole',
      isOwner: false,
      joinedAtUtc: '2026-01-01T00:00:00Z',
    };
    mockCustomInstance.mockResolvedValue(result);

    const response = await updateUserRole('tenant-1', 'u1', {
      role: 'CustomRole',
    });

    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/tenants/tenant-1/users/u1/role',
      method: 'PUT',
      data: {
        role: 'CustomRole',
      },
    });
    expect(response).toEqual(result);
  });

  it('propagates API errors', async () => {
    mockCustomInstance.mockRejectedValue(new Error('404 Not Found'));
    await expect(updateUserRole('tenant-1', 'u1', { role: 'Manager' })).rejects.toThrow(
      '404 Not Found'
    );
  });
});
