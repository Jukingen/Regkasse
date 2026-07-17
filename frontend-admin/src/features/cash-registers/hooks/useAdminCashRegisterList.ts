'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import {
    adminCashRegisterListQueryKey,
    listAdminCashRegisters,
    toEnhancedCashRegister,
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
    /** Tenant Manager: omit tenantId and let the API resolve the JWT tenant. */
    allowTenantScopedDefault?: boolean;
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
        allowTenantScopedDefault = false,
        pageSize = 100,
        excludeDecommissioned = true,
        enabled = true,
        pollIntervalMs = 30_000,
    } = options;

    const listParams = useMemo((): ListAdminCashRegistersParams | null => {
        const excludeStatus = excludeDecommissioned ? 'Decommissioned' : undefined;
        if (tenantId) {
            return { tenantId, page: 1, pageSize, excludeStatus };
        }
        if (allowAllTenants) {
            return { page: 1, pageSize, listScope: 'all', excludeStatus };
        }
        if (allowTenantScopedDefault) {
            return { page: 1, pageSize, listScope: 'jwt', excludeStatus };
        }
        return null;
    }, [allowAllTenants, allowTenantScopedDefault, excludeDecommissioned, pageSize, tenantId]);

    const query = useQuery({
        queryKey: adminCashRegisterListQueryKey(listParams ?? undefined),
        queryFn: () => listAdminCashRegisters(listParams!),
        enabled: enabled && listParams != null,
        staleTime: 15_000,
        refetchInterval: enabled && listParams != null ? pollIntervalMs : false,
    });

    const registers = useMemo(() => {
        const items = (query.data?.items ?? []).map(toEnhancedCashRegister);
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
