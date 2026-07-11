'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
    closeCashRegisterShift,
    fetchShiftStatus,
    openCashRegisterShift,
    shiftStatusQueryKey,
    type ShiftStatusDto,
} from '@/features/shifts/api/shiftManagement';
import { invalidateShiftRelatedQueries } from '@/features/shifts/api/shiftQueryInvalidation';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

/**
 * Manager quick actions: register open/close (operational shift) for one cash register.
 * Status via tenant register list; mutations use canonical cash-register open/close APIs.
 */
export function useShiftManagement(cashRegisterId?: string) {
    const queryClient = useQueryClient();
    const { message } = useAntdApp();
    const { t } = useI18n();
    const trimmedRegisterId = cashRegisterId?.trim() ?? '';

    const { data: shiftStatus } = useQuery({
        queryKey: shiftStatusQueryKey(trimmedRegisterId),
        queryFn: () => fetchShiftStatus(trimmedRegisterId),
        enabled: Boolean(trimmedRegisterId),
    });

    const openShiftMutation = useMutation({
        mutationFn: (registerId: string) => openCashRegisterShift(registerId),
        onSuccess: async (_data, registerId) => {
            await invalidateShiftRelatedQueries(queryClient, registerId);
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'useShiftManagement.openShift',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const closeShiftMutation = useMutation({
        mutationFn: async (registerId: string) => {
            const id = registerId.trim();
            const cached = queryClient.getQueryData<ShiftStatusDto>(shiftStatusQueryKey(id));
            const status = cached ?? (await fetchShiftStatus(id));
            return closeCashRegisterShift(id, status.currentBalance);
        },
        onSuccess: async (_data, registerId) => {
            await invalidateShiftRelatedQueries(queryClient, registerId);
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'useShiftManagement.closeShift',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    return {
        shiftStatus,
        isShiftOpen: shiftStatus?.isOpen ?? false,
        openShift: openShiftMutation.mutateAsync,
        closeShift: closeShiftMutation.mutateAsync,
        isLoading: openShiftMutation.isPending || closeShiftMutation.isPending,
    };
}
