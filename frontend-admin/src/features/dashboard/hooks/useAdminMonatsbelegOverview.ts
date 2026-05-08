'use client';

import { useMemo } from 'react';
import type { CashRegister } from '@/api/generated/model';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { useQueries } from '@tanstack/react-query';
import { getMonatsbelegStatus, type MonatsbelegStatusDto } from '@/features/dashboard/api/monatsbelegStatus';

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
 * Loads tenant cash registers (GET /api/CashRegister) and Monatsbeleg status per register
 * (GET /api/rksv/monatsbeleg/status/{id}). Cached & deduped via TanStack Query.
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
            refetchOnWindowFocus: true,
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

    const statusQueries = useQueries({
        queries: registerIds.map((id) => ({
            queryKey: ['rksv', 'monatsbeleg-status', id] as const,
            queryFn: () => getMonatsbelegStatus(id),
            enabled: enabled && registerIds.length > 0 && !registersLoading && !registersError,
            staleTime: FIVE_MIN_MS,
            refetchInterval: FIVE_MIN_MS,
            refetchOnWindowFocus: true,
        })),
    });

    const rows: RegisterMonatsbelegRow[] = useMemo(() => {
        const out: RegisterMonatsbelegRow[] = [];
        for (let index = 0; index < registerIds.length; index++) {
            const id = registerIds[index];
            const register = registers.find((r) => r.id === id);
            if (!register?.id) continue;
            const q = statusQueries[index];
            out.push({
                register,
                registerId: id,
                status: q?.data,
                statusError: q?.isError ?? false,
                statusLoading: q?.isPending ?? false,
            });
        }
        return out;
    }, [registerIds, registers, statusQueries]);

    const redCount = useMemo(() => rows.filter((r) => r.status?.missingMonths?.some((m) => m.isOverdue)).length, [rows]);

    const statusPending = statusQueries.some((q) => q.isPending || q.isLoading);

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
