'use client';

import { useMemo } from 'react';

import { useOpenShifts } from '@/features/shifts/hooks/useOpenShifts';

export type ActiveStaffMember = {
    cashierId: string;
    cashierName: string;
};

/**
 * Distinct cashiers currently on open shifts (optionally for one register).
 */
export function useActiveStaff(cashRegisterId?: string) {
    const shiftsQuery = useOpenShifts(cashRegisterId);

    const data = useMemo((): ActiveStaffMember[] => {
        const byId = new Map<string, ActiveStaffMember>();
        for (const shift of shiftsQuery.data) {
            if (!shift.cashierId || byId.has(shift.cashierId)) {
                continue;
            }
            byId.set(shift.cashierId, {
                cashierId: shift.cashierId,
                cashierName: shift.cashierName,
            });
        }
        return Array.from(byId.values());
    }, [shiftsQuery.data]);

    return {
        data,
        isLoading: shiftsQuery.isLoading,
        isFetching: shiftsQuery.isFetching,
        isError: shiftsQuery.isError,
        error: shiftsQuery.error,
        refetch: shiftsQuery.refetch,
    };
}
