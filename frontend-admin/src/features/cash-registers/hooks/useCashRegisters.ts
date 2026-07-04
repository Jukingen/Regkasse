'use client';

import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';

import {
    cashRegisterByTenantQueryKey,
    listCashRegistersByTenant,
    type AdminCashRegisterListItem,
} from '@/features/cash-registers/api/cashRegisters';
import {
    readQuickCashRegisterId,
    writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';
import { pickPreferredCashRegisterId } from '@/features/cash-registers/utils/pickPreferredCashRegister';

export type UseCashRegistersOptions = {
    enabled?: boolean;
    /** Persist header quick-switch selection when tenant registers load. */
    syncQuickSwitch?: boolean;
};

export function useCashRegisters(tenantId?: string, options: UseCashRegistersOptions = {}) {
    const { enabled = true, syncQuickSwitch = false } = options;

    const query = useQuery({
        queryKey: cashRegisterByTenantQueryKey(tenantId),
        queryFn: () => listCashRegistersByTenant(),
        enabled: enabled && Boolean(tenantId),
        staleTime: 15_000,
    });

    const registers = query.data ?? [];

    const defaultRegister = useMemo(
        (): AdminCashRegisterListItem | null =>
            registers.find((row) => row.isDefaultForTenant === true) ?? registers[0] ?? null,
        [registers],
    );

    const [selectedRegisterId, setSelectedRegisterId] = useState<string | null>(null);

    useEffect(() => {
        if (!syncQuickSwitch) {
            return;
        }

        if (query.isLoading) {
            return;
        }

        if (!tenantId || registers.length === 0) {
            setSelectedRegisterId(null);
            writeQuickCashRegisterId(null, tenantId ?? null);
            return;
        }

        const storedId = readQuickCashRegisterId(tenantId);
        const preferred = pickPreferredCashRegisterId(registers, storedId, tenantId);
        if (preferred !== storedId) {
            writeQuickCashRegisterId(preferred, tenantId);
        }
        setSelectedRegisterId(preferred);
    }, [query.isLoading, registers, syncQuickSwitch, tenantId]);

    return {
        registers,
        defaultRegister,
        selectedRegisterId: syncQuickSwitch ? selectedRegisterId : null,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        error: query.error,
        refetch: query.refetch,
    };
}
