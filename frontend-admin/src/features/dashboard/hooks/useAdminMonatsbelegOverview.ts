'use client';

import { useMemo } from 'react';
import type { CashRegister } from '@/api/generated/model';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { useQuery } from '@tanstack/react-query';
import {
    getApiRksvMonatsbelegStatusOverview,
    getGetApiRksvMonatsbelegStatusOverviewQueryKey,
} from '@/api/generated/rksv/rksv';
import type { MonatsbelegStatusDto } from '@/api/generated/model';

const FIVE_MIN_MS = 5 * 60 * 1000;

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
        isError: registersError,
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

    const overviewQuery = useQuery({
        queryKey: getGetApiRksvMonatsbelegStatusOverviewQueryKey(),
        queryFn: () => getApiRksvMonatsbelegStatusOverview(),
        enabled: enabled && registerIds.length > 0 && !registersLoading && !registersError,
        staleTime: FIVE_MIN_MS,
        refetchInterval: FIVE_MIN_MS,
        refetchIntervalInBackground: false,
        refetchOnWindowFocus: false,
    });

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
                statusError: overviewQuery.isError,
                statusLoading: overviewQuery.isPending || overviewQuery.isLoading,
            });
        }
        return out;
    }, [registerIds, registers, statusByRegisterId, overviewQuery.isError, overviewQuery.isPending, overviewQuery.isLoading]);

    const redCount = useMemo(() => rows.filter((r) => r.status?.missingMonths?.some((m) => m.isOverdue)).length, [rows]);

    const statusPending = overviewQuery.isPending || overviewQuery.isLoading;

    return {
        registersLoading,
        registersError,
        refetchRegisters,
        rows,
        redCount,
        statusPending,
        hasRegisters: registerIds.length > 0,
    };
}
