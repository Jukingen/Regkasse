/**
 * usePermissionsCatalog – load success, error, empty, enabled: false.
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { usePermissionsCatalog } from '../usePermissionsCatalog';
import type { PermissionCatalogItemDto } from '../../api/usersGateway';

const mockGetPermissionsCatalog = vi.fn();

vi.mock('../../api/usersGateway', () => ({
  getPermissionsCatalog: (params?: unknown) => mockGetPermissionsCatalog(params),
  permissionsCatalogQueryKey: ['/api/UserManagement/roles/permissions-catalog'] as const,
}));

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

const sampleCatalog: PermissionCatalogItemDto[] = [
  { key: 'user.view', group: 'User', resource: 'user', action: 'view' },
  { key: 'sale.view', group: 'Sale', resource: 'sale', action: 'view' },
];

describe('usePermissionsCatalog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns catalog when load succeeds', async () => {
    mockGetPermissionsCatalog.mockResolvedValue(sampleCatalog);
    const wrapper = createWrapper();
    const { result } = renderHook(() => usePermissionsCatalog({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(sampleCatalog);
    expect(result.current.data).toHaveLength(2);
  });

  it('returns empty array when API returns empty', async () => {
    mockGetPermissionsCatalog.mockResolvedValue([]);
    const wrapper = createWrapper();
    const { result } = renderHook(() => usePermissionsCatalog({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual([]);
  });

  it('sets isError when getPermissionsCatalog rejects', async () => {
    mockGetPermissionsCatalog.mockRejectedValue(new Error('Network error'));
    const wrapper = createWrapper();
    const { result } = renderHook(() => usePermissionsCatalog({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
  });

  it('does not fetch when enabled is false', async () => {
    const wrapper = createWrapper();
    renderHook(() => usePermissionsCatalog({ enabled: false }), { wrapper });
    await waitFor(() => {}, { timeout: 100 }).catch(() => {});
    expect(mockGetPermissionsCatalog).not.toHaveBeenCalled();
  });
});
