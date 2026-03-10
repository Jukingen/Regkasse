/**
 * useRolesWithPermissions – load success, error, empty, enabled: false.
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRolesWithPermissions } from '../useRolesWithPermissions';
import type { RoleWithPermissionsDto } from '../../api/usersGateway';

const mockGetRolesWithPermissions = vi.fn();

vi.mock('../../api/usersGateway', () => ({
  getRolesWithPermissions: (params?: unknown) => mockGetRolesWithPermissions(params),
  rolesWithPermissionsQueryKey: ['/api/UserManagement/roles/with-permissions'] as const,
}));

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

const sampleRoles: RoleWithPermissionsDto[] = [
  { roleName: 'Admin', permissions: ['user.view'], isSystemRole: true, userCount: 2 },
  { roleName: 'Custom', permissions: ['sale.view'], isSystemRole: false, userCount: 0 },
];

describe('useRolesWithPermissions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns roles when load succeeds', async () => {
    mockGetRolesWithPermissions.mockResolvedValue(sampleRoles);
    const wrapper = createWrapper();
    const { result } = renderHook(() => useRolesWithPermissions({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sampleRoles);
    expect(result.current.data).toHaveLength(2);
  });

  it('returns empty array when API returns empty', async () => {
    mockGetRolesWithPermissions.mockResolvedValue([]);
    const wrapper = createWrapper();
    const { result } = renderHook(() => useRolesWithPermissions({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual([]);
  });

  it('sets isError when getRolesWithPermissions rejects', async () => {
    mockGetRolesWithPermissions.mockRejectedValue(new Error('Network error'));
    const wrapper = createWrapper();
    const { result } = renderHook(() => useRolesWithPermissions({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
  });

  it('does not fetch when enabled is false', async () => {
    const wrapper = createWrapper();
    renderHook(() => useRolesWithPermissions({ enabled: false }), { wrapper });
    await waitFor(() => {}, { timeout: 100 }).catch(() => {});
    expect(mockGetRolesWithPermissions).not.toHaveBeenCalled();
  });
});
