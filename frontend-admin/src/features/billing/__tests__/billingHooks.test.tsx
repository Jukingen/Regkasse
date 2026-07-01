import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { useBillingSalesList, useBillingCreate } from '../hooks';
import type { LicenseSaleListResponse, LicenseSaleResponse } from '../api/billingApi';

const { mockGetSalesList, mockUseCreate, mockMutateAsync } = vi.hoisted(() => ({
    mockGetSalesList: vi.fn(),
    mockUseCreate: vi.fn(),
    mockMutateAsync: vi.fn(),
}));

vi.mock('@/api/generated/admin/admin', () => ({
    getApiAdminBillingLicenseSales: (...args: unknown[]) => mockGetSalesList(...args),
}));

vi.mock('../api/billingApi', () => ({
    billingApi: {
        useCreate: (...args: unknown[]) => mockUseCreate(...args),
    },
}));

vi.mock('../hooks/useBillingAccess', () => ({
    useBillingAccess: () => true,
}));

function createWrapper() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    return function Wrapper({ children }: { children: ReactNode }) {
        return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
    };
}

describe('Billing Hooks', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockUseCreate.mockReturnValue({
            mutateAsync: mockMutateAsync,
            isPending: false,
        });
    });

    it('useBillingSalesList fetches sales list', async () => {
        const mockData: LicenseSaleListResponse = {
            items: [
                {
                    id: '1',
                    invoiceNumber: 'RE20260841',
                    status: 'active',
                },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 20,
            totalPages: 1,
        };

        mockGetSalesList.mockResolvedValue(mockData);

        const { result } = renderHook(() => useBillingSalesList({ page: 1, pageSize: 20 }), {
            wrapper: createWrapper(),
        });

        await waitFor(() => expect(result.current.data).toBeDefined());
        expect(result.current.data).toEqual(mockData);
        expect(mockGetSalesList).toHaveBeenCalledWith({
            page: 1,
            pageSize: 20,
            tenantId: undefined,
            search: undefined,
            status: undefined,
            fromDate: undefined,
            toDate: undefined,
        });
    });

    it('useBillingSalesList normalizes search and status filters', async () => {
        mockGetSalesList.mockResolvedValue({ items: [], totalCount: 0 });

        renderHook(
            () =>
                useBillingSalesList({
                    page: 2,
                    pageSize: 10,
                    search: '  cafe  ',
                    status: 'all',
                }),
            { wrapper: createWrapper() },
        );

        await waitFor(() =>
            expect(mockGetSalesList).toHaveBeenCalledWith({
                page: 2,
                pageSize: 10,
                tenantId: undefined,
                search: 'cafe',
                status: undefined,
                fromDate: undefined,
                toDate: undefined,
            }),
        );
    });

    it('useBillingCreate creates a new sale', async () => {
        const mockSale: LicenseSaleResponse = {
            id: '1',
            invoiceNumber: 'RE20260841',
            status: 'active',
            priceNet: 299.0,
        };

        mockMutateAsync.mockResolvedValue(mockSale);

        const { result } = renderHook(() => useBillingCreate(), {
            wrapper: createWrapper(),
        });

        const createData = {
            tenantId: 'tenant-1',
            licensePlan: '12_months',
            priceNet: 299.0,
            vatRate: 20.0,
        };

        await result.current.mutateAsync({ data: createData });

        expect(mockMutateAsync).toHaveBeenCalledWith({ data: createData });
        expect(mockUseCreate).toHaveBeenCalledWith(
            expect.objectContaining({
                mutation: expect.objectContaining({
                    onSuccess: expect.any(Function),
                }),
            }),
        );
    });
});
