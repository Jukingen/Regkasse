'use client';

import { useCallback, useMemo } from 'react';
import type { CashRegister } from '@/api/generated/model';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import type { MonatsbelegStatusDto } from '@/api/generated/model';
import {
    MONATSBELEG_REFETCH_INTERVAL_MS,
    useMonatsbelegStatus,
} from '@/features/rksv/hooks/useMonatsbeleg';

const FIVE_MIN_MS = MONATSBELEG_REFETCH_INTERVAL_MS;

function normalizeRegisterRows(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const r = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(r)) return r;
    }
    return [];
}

export type RegisterMonatsbelegRow = {
    register: CashRegister;
    registerId: string;
    status?: MonatsbelegStatusDto;
    statusError: boolean;
    statusLoading: boolean;
};

/**
 * Loads tenant cash registers and Monatsbeleg status via GET /api/rksv/monatsbeleg/status-overview.
 */
export function useAdminMonatsbelegOverview(enabled = true) {
    const {
        data: registersRaw,
        isLoading: registersLoading,
        isFetching: registersFetching,
        isError: registersError,
        error: registersQueryError,
        refetch: refetchRegisters,
    } = useGetApiCashRegister({
        query: {
            enabled,
            staleTime: FIVE_MIN_MS,
            refetchInterval: FIVE_MIN_MS,
            refetchIntervalInBackground: false,
            refetchOnWindowFocus: false,
        },
    });

    const registers = useMemo(() => normalizeRegisterRows(registersRaw), [registersRaw]);

    const registerIds = useMemo(
        () =>
            registers
                .map((r) => r.id?.trim())
                .filter((id): id is string => Boolean(id && id.length > 0)),
        [registers],
    );

    const overviewEnabled = enabled && registerIds.length > 0 && !registersLoading && !registersError;

    const overviewQuery = useMonatsbelegStatus({ enabled: overviewEnabled });

    // Disabled queries stay isPending in TanStack Query v5; only treat active fetches as loading.
    const statusPending = overviewQuery.fetchStatus === 'fetching';
    const loadError = registersError || overviewQuery.isError;

    const statusByRegisterId = useMemo(() => {
        const map = new Map<string, MonatsbelegStatusDto>();
        for (const item of overviewQuery.data ?? []) {
            const id = item.cashRegisterId?.trim();
            if (id && item.status) map.set(id, item.status);
        }
        return map;
    }, [overviewQuery.data]);

    const rows: RegisterMonatsbelegRow[] = useMemo(() => {
        const out: RegisterMonatsbelegRow[] = [];
        for (const id of registerIds) {
            const register = registers.find((r) => r.id === id);
            if (!register?.id) continue;
            out.push({
                register,
                registerId: id,
                status: statusByRegisterId.get(id),
                statusError: loadError,
                statusLoading: statusPending,
            });
        }
        return out;
    }, [loadError, registerIds, registers, statusByRegisterId, statusPending]);

    const redCount = useMemo(() => rows.filter((r) => r.status?.missingMonths?.some((m) => m.isOverdue)).length, [rows]);

    const refetchAll = useCallback(async () => {
        await refetchRegisters();
        if (overviewEnabled) {
            await overviewQuery.refetch();
        }
    }, [overviewEnabled, overviewQuery.refetch, refetchRegisters]);

    return {
        registersLoading,
        registersFetching,
        registersError,
        registersQueryError,
        loadError,
        overviewError: overviewQuery.error,
        refetchRegisters,
        refetchOverview: overviewQuery.refetch,
        refetchAll,
        rows,
        redCount,
        statusPending,
        hasRegisters: registerIds.length > 0,
    };
}
