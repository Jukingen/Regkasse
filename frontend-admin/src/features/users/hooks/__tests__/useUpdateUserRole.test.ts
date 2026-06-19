/**
 * useUpdateUserRole – mutation forwards preservePreviousPermissions to API.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useUpdateUserRole } from '../useUpdateUserRole';

const mockUpdateUserRole = vi.fn();

vi.mock('@/features/users/api/users', () => ({
    updateUserRole: (...args: unknown[]) => mockUpdateUserRole(...args),
    adminUsersQueryKeys: {
        all: () => ['admin', 'users'],
        tenant: () => ['admin', 'users', 'tenant'],
    },
}));

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { mutations: { retry: false } },
    });
    return function Wrapper({ children }: { children: React.ReactNode }) {
        return React.createElement(QueryClientProvider, { client: queryClient }, children);
    };
}

describe('useUpdateUserRole', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('calls updateUserRole with preservePreviousPermissions when checked', async () => {
        mockUpdateUserRole.mockResolvedValue({ userId: 'u1', role: 'CustomRole' });
        const onSuccess = vi.fn();
        const { result } = renderHook(() => useUpdateUserRole({ onSuccess }), {
            wrapper: createWrapper(),
        });

        result.current.mutate({
            tenantId: 'tenant-1',
            userId: 'u1',
            role: 'CustomRole',
            preservePreviousPermissions: true,
        });

        await waitFor(() => {
            expect(result.current.isSuccess).toBe(true);
        });

        expect(mockUpdateUserRole).toHaveBeenCalledWith('tenant-1', 'u1', {
            role: 'CustomRole',
            preservePreviousPermissions: true,
        });
        expect(onSuccess).toHaveBeenCalled();
    });

    it('defaults preservePreviousPermissions to undefined in API call', async () => {
        mockUpdateUserRole.mockResolvedValue({ userId: 'u1', role: 'Cashier' });
        const { result } = renderHook(() => useUpdateUserRole(), {
            wrapper: createWrapper(),
        });

        result.current.mutate({
            tenantId: 'tenant-1',
            userId: 'u1',
            role: 'Cashier',
        });

        await waitFor(() => {
            expect(result.current.isSuccess).toBe(true);
        });

        expect(mockUpdateUserRole).toHaveBeenCalledWith('tenant-1', 'u1', {
            role: 'Cashier',
            preservePreviousPermissions: undefined,
        });
    });
});
