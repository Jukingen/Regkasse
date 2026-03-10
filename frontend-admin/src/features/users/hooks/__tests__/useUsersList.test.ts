/**
 * useUsersList – liste yükleme (başarılı/boş/hatalı) ve parametre iletimi.
 * Gateway mock ile gerçek endpoint şekli kullanılır.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import React from 'react';
import { useUsersList } from '../useUsersList';
import type { UserInfo } from '@/api/generated/model';
import type { UsersListResponse } from '../../api/usersApi';

const mockGetUsersList = vi.fn();

vi.mock('../../api/usersGateway', () => ({
  getUsersList: (params: unknown) => mockGetUsersList(params),
  listQueryKey: ['/api/UserManagement'] as const,
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

const sampleUser: UserInfo = {
  id: 'u1',
  userName: 'jane',
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  role: 'SuperAdmin',
  isActive: true,
  employeeNumber: 'E001',
  lastLoginAt: '2025-01-15T10:00:00Z',
};

const listResponse = (
  items: UserInfo[],
  page = 1,
  pageSize = 20,
  totalCount = items.length
): UsersListResponse => ({
  items,
  pagination: {
    page,
    pageSize,
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize) || 1,
  },
});

describe('useUsersList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls getUsersList with default page and pageSize when no params given', async () => {
    mockGetUsersList.mockResolvedValue(listResponse([]));
    const wrapper = createWrapper();
    renderHook(() => useUsersList(undefined, { enabled: true }), {
      wrapper,
    });
    await waitFor(() => {
      expect(mockGetUsersList).toHaveBeenCalledWith(undefined);
    });
  });

  it('returns items and pagination when list load succeeds', async () => {
    const response = listResponse([sampleUser], 1, 20, 1);
    mockGetUsersList.mockResolvedValue(response);
    const wrapper = createWrapper();
    const { result } = renderHook(() => useUsersList({}, { enabled: true }), {
      wrapper,
    });
    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });
    expect(result.current.data).toEqual(response);
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.pagination.totalCount).toBe(1);
  });

  it('shows empty list when API returns zero items', async () => {
    mockGetUsersList.mockResolvedValue(listResponse([], 1, 20, 0));
    const wrapper = createWrapper();
    const { result } = renderHook(() => useUsersList({ page: 1 }, { enabled: true }), {
      wrapper,
    });
    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });
    expect(result.current.data?.items).toHaveLength(0);
    expect(result.current.data?.pagination.totalCount).toBe(0);
  });

  it('sets isError when getUsersList rejects', async () => {
    mockGetUsersList.mockRejectedValue(new Error('Network error'));
    const wrapper = createWrapper();
    const { result } = renderHook(() => useUsersList({}, { enabled: true }), {
      wrapper,
    });
    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });
    expect(result.current.error).toBeDefined();
  });

  it('passes role, isActive and query to getUsersList', async () => {
    mockGetUsersList.mockResolvedValue(listResponse([]));
    const wrapper = createWrapper();
    renderHook(
      () =>
        useUsersList(
          {
            role: 'SuperAdmin',
            isActive: true,
            query: 'jane',
            page: 2,
            pageSize: 10,
          },
          { enabled: true }
        ),
      { wrapper }
    );
    await waitFor(() => {
      expect(mockGetUsersList).toHaveBeenCalledWith({
        role: 'SuperAdmin',
        isActive: true,
        query: 'jane',
        page: 2,
        pageSize: 10,
      });
    });
  });

  it('does not fetch when enabled is false', async () => {
    const wrapper = createWrapper();
    renderHook(() => useUsersList({}, { enabled: false }), { wrapper });
    await waitFor(() => {}, { timeout: 100 }).catch(() => {});
    expect(mockGetUsersList).not.toHaveBeenCalled();
  });
});
