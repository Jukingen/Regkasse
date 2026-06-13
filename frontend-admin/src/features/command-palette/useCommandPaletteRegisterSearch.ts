'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import {
    adminCashRegisterListQueryKey,
    cashRegisterByTenantQueryKey,
    listAdminCashRegisters,
    listCashRegistersByTenant,
} from '@/features/cash-registers/api/cashRegisters';
import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { useDebounce } from '@/hooks/useDebounce';
import type { CommandItem } from '@/features/command-palette/types';

const MIN_QUERY_LEN = 2;
const MAX_RESULTS = 5;
const DEBOUNCE_MS = 300;

function filterRegisters(registers: AdminCashRegisterListItem[], trimmed: string): AdminCashRegisterListItem[] {
    return registers.filter((reg) => {
        const hay = [reg.id, reg.registerNumber, reg.location].filter(Boolean).join(' ').toLowerCase();
        return hay.includes(trimmed);
    });
}

export function useCommandPaletteRegisterSearch(
    query: string,
    options: { enabled: boolean; isSuperAdmin: boolean; closePalette: () => void },
): { items: CommandItem[]; isLoading: boolean } {
    const router = useRouter();
    const debouncedQuery = useDebounce(query.trim().toLowerCase(), DEBOUNCE_MS);
    const enabled = options.enabled && debouncedQuery.length >= MIN_QUERY_LEN;

    const tenantQuery = useQuery({
        queryKey: cashRegisterByTenantQueryKey(undefined),
        queryFn: () => listCashRegistersByTenant(),
        enabled: enabled && !options.isSuperAdmin,
        staleTime: 30_000,
    });

    const adminQuery = useQuery({
        queryKey: adminCashRegisterListQueryKey({ page: 1, pageSize: 100 }),
        queryFn: () => listAdminCashRegisters({ page: 1, pageSize: 100 }),
        enabled: enabled && options.isSuperAdmin,
        staleTime: 30_000,
    });

    const active = options.isSuperAdmin ? adminQuery : tenantQuery;

    const items = useMemo((): CommandItem[] => {
        if (debouncedQuery.length < MIN_QUERY_LEN) return [];

        const rows: AdminCashRegisterListItem[] = options.isSuperAdmin
            ? (adminQuery.data?.items ?? [])
            : (tenantQuery.data ?? []);

        const matches = filterRegisters(rows, debouncedQuery);

        return matches.slice(0, MAX_RESULTS).map((reg) => {
            const label = reg.registerNumber?.trim() || reg.id || '—';
            return {
                id: `register:${reg.id}`,
                type: 'register' as const,
                label,
                description: reg.location ?? reg.id,
                group: 'Registers' as const,
                keywords: [reg.id ?? '', reg.registerNumber ?? '', reg.location ?? '', label],
                action: () => {
                    options.closePalette();
                    router.push(`/kassenverwaltung?registerId=${encodeURIComponent(reg.id ?? '')}`);
                },
            };
        });
    }, [
        debouncedQuery,
        options.isSuperAdmin,
        options.closePalette,
        adminQuery.data?.items,
        tenantQuery.data,
        router,
    ]);

    return { items, isLoading: active.isFetching };
}
