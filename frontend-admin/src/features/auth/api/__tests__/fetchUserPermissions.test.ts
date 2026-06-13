import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fetchUserPermissions } from '@/features/auth/api/fetchUserPermissions';

const mockFetchAuthUser = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  fetchAuthUser: () => mockFetchAuthUser(),
  AUTH_KEYS: { user: ['auth', 'me'] },
}));

describe('fetchUserPermissions', () => {
  beforeEach(() => {
    mockFetchAuthUser.mockReset();
  });

  it('returns permissions from /me mapping', async () => {
    mockFetchAuthUser.mockResolvedValue({
      id: 'u1',
      permissions: ['settings.view', 'product.view'],
    });

    await expect(fetchUserPermissions()).resolves.toEqual(['settings.view', 'product.view']);
  });

  it('returns empty array when /me fails', async () => {
    mockFetchAuthUser.mockRejectedValue(new Error('network'));

    await expect(fetchUserPermissions()).resolves.toEqual([]);
  });
});
