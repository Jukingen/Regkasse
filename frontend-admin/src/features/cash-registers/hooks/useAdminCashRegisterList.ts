'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import {
    adminCashRegisterListQueryKey,
    listAdminCashRegisters,
    type ListAdminCashRegistersParams,
} from '@/features/cash-registers/api/cashRegisters';
import {
    isDecommissionedRegister,
    rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import type { CashRegister } from '@/api/generated/model';

export type UseAdminCashRegisterListOptions = {
    tenantId?: string;
    /** Super Admin without tenantId loads all mandants. */
    allowAllTenants?: boolean;
    pageSize?: number;
    excludeDecommissioned?: boolean;
    enabled?: boolean;
    /** Poll list for offline queue / sync updates (default 30s when enabled). */
    pollIntervalMs?: number;
};

export function useAdminCashRegisterList(options: UseAdminCashRegisterListOptions) {
    const {
        tenantId,
        allowAllTenants = false,
        pageSize = 100,
        excludeDecommissioned = true,
        enabled = true,
        pollIntervalMs = 30_000,
    } = options;

    const listParams = useMemo((): ListAdminCashRegistersParams | null => {
        if (tenantId) {
            return { tenantId, page: 1, pageSize };
        }
        if (allowAllTenants) {
            return { page: 1, pageSize };
        }
        return null;
    }, [allowAllTenants, pageSize, tenantId]);

    const query = useQuery({
        queryKey: adminCashRegisterListQueryKey(listParams ?? undefined),
        queryFn: () => listAdminCashRegisters(listParams!),
        enabled: enabled && listParams != null,
        staleTime: 15_000,
        refetchInterval: enabled && listParams != null ? pollIntervalMs : false,
    });

    const registers = useMemo(() => {
        const items = query.data?.items ?? [];
        if (!excludeDecommissioned) {
            return items;
        }
        return items.filter(
            (row) => !isDecommissionedRegister(rawRegisterStatus(row as unknown as CashRegister)),
        );
    }, [excludeDecommissioned, query.data?.items]);

    return {
        registers,
        totalCount: query.data?.totalCount ?? 0,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        error: query.error,
        refetch: query.refetch,
    };
}
