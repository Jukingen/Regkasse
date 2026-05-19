'use client';

import { useMemo } from 'react';
import type { CashRegister } from '@/api/generated/model';
import type { RksvReminderStatusDto } from '@/api/generated/model';
import { useGetApiCashRegister } from '@/api/generated/cash-register/cash-register';
import { getApiRksvReminderStatusCashRegisterId } from '@/api/generated/rksv/rksv';
import { useQueries } from '@tanstack/react-query';

const FIVE_MIN_MS = 5 * 60 * 1000;

function normalizeRegisterRows(data: unknown): CashRegister[] {
    if (Array.isArray(data)) return data as CashRegister[];
    if (data && typeof data === 'object' && 'registers' in data) {
        const r = (data as { registers?: CashRegister[] }).registers;
        if (Array.isArray(r)) return r;
    }
    return [];
}

export type RegisterReminderRow = {
    register: CashRegister;
    registerId: string;
    status?: RksvReminderStatusDto;
    statusError: boolean;
    statusLoading: boolean;
};

export function isStartbelegMissing(status: RksvReminderStatusDto | undefined): boolean {
    return status?.startbeleg?.status === 'missing' || status?.startbeleg?.isRequired === true;
}

export function isJahresbelegActionRequired(status: RksvReminderStatusDto | undefined): boolean {
    const jb = status?.jahresbeleg;
    if (!jb?.isRequired) return false;
    return jb.status === 'overdue' || jb.status === 'upcoming';
}

export function isMonatsbelegActionRequired(status: RksvReminderStatusDto | undefined): boolean {
    const mb = status?.monatsbeleg;
    if (!mb) return false;
    return mb.status === 'overdue' || mb.status === 'upcoming' || mb.isRequired === true;
}

/**
 * Per-register unified RKSV reminder status (GET /api/rksv/reminder/status/{id}).
 */
export function useRksvReminderOverview(enabled = true) {
    const {
        data: registersRaw,
        isLoading: registersLoading,
        isError: registersError,
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
            queryKey: ['rksv', 'reminder-status', id] as const,
            queryFn: () => getApiRksvReminderStatusCashRegisterId(id),
            enabled: enabled && registerIds.length > 0 && !registersLoading && !registersError,
            staleTime: FIVE_MIN_MS,
            refetchInterval: FIVE_MIN_MS,
            refetchOnWindowFocus: true,
        })),
    });

    const rows: RegisterReminderRow[] = useMemo(() => {
        const out: RegisterReminderRow[] = [];
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

    const summary = useMemo(() => {
        let startbelegMissingCount = 0;
        let jahresbelegAttentionCount = 0;
        let monatsbelegAttentionCount = 0;
        for (const row of rows) {
            if (row.statusLoading || row.statusError) continue;
            if (isStartbelegMissing(row.status)) startbelegMissingCount += 1;
            if (isJahresbelegActionRequired(row.status)) jahresbelegAttentionCount += 1;
            if (isMonatsbelegActionRequired(row.status) && row.status?.monatsbeleg?.status === 'overdue') {
                monatsbelegAttentionCount += 1;
            }
        }
        return { startbelegMissingCount, jahresbelegAttentionCount, monatsbelegAttentionCount };
    }, [rows]);

    const statusPending = statusQueries.some((q) => q.isPending || q.isLoading);

    return {
        registersLoading,
        registersError,
        rows,
        summary,
        statusPending,
        hasRegisters: registerIds.length > 0,
    };
}
