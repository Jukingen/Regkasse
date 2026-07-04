'use client';

import { useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { CashRegister } from '@/api/generated/model';
import {
    postApiCashRegisterIdClose,
    postApiCashRegisterIdOpen,
} from '@/api/generated/cash-register/cash-register';
import type { CashRegisterActionKey } from '@/features/cash-registers/components/CashRegisterActions';
import { cashRegisterListQueryKey } from '@/features/cash-registers/api/cashRegisters';
import { FA_QUICK_CASH_REGISTER_QUERY_PARAM } from '@/features/cash-registers/constants/quickSwitch';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { getUserFacingApiErrorMessage } from '@/shared/errors/userFacingApiError';

type UseCashRegisterActionHandlerOptions = {
    onEdit: (register: CashRegister) => void;
    onDecommission: (register: CashRegister) => void;
    onHardDelete: (register: CashRegister) => void;
};

export function useCashRegisterActionHandler({
    onEdit,
    onDecommission,
    onHardDelete,
}: UseCashRegisterActionHandlerOptions) {
    const { message } = useAntdApp();
    const { t } = useI18n();
    const router = useRouter();
    const queryClient = useQueryClient();

    const invalidateRegisters = useCallback(async () => {
        await Promise.all([
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers'] }),
            queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey }),
            queryClient.invalidateQueries({ queryKey: ['cash-registers'] }),
            queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] }),
        ]);
    }, [queryClient]);

    const openMutation = useMutation({
        mutationFn: (register: CashRegister) =>
            postApiCashRegisterIdOpen(register.id!.trim(), { openingBalance: 0 }),
        onSuccess: async () => {
            message.success(t('cashRegisters.shift.openSuccess'));
            await invalidateRegisters();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'CashRegisterActions.openShift',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const closeMutation = useMutation({
        mutationFn: (register: CashRegister) =>
            postApiCashRegisterIdClose(register.id!.trim(), {
                closingBalance: register.currentBalance ?? 0,
            }),
        onSuccess: async () => {
            message.success(t('cashRegisters.shift.closeSuccess'));
            await invalidateRegisters();
        },
        onError: (err) => {
            message.error(
                getUserFacingApiErrorMessage(t, err, {
                    logContext: 'CashRegisterActions.closeShift',
                    fallbackKey: 'common.messages.unknownError',
                }),
            );
        },
    });

    const handleRegisterAction = useCallback(
        (key: CashRegisterActionKey, register: CashRegister) => {
            const registerId = register.id?.trim();
            if (!registerId) {
                return;
            }

            switch (key) {
                case 'open-shift':
                    if (openMutation.isPending) return;
                    openMutation.mutate(register);
                    break;
                case 'close-shift':
                    if (closeMutation.isPending) return;
                    closeMutation.mutate(register);
                    break;
                case 'daily-closing':
                    router.push(
                        `/tagesabschluss?${FA_QUICK_CASH_REGISTER_QUERY_PARAM}=${encodeURIComponent(registerId)}`,
                    );
                    break;
                case 'edit':
                    onEdit(register);
                    break;
                case 'delete':
                    onHardDelete(register);
                    break;
                case 'decommission':
                    onDecommission(register);
                    break;
                default:
                    break;
            }
        },
        [closeMutation, onDecommission, onEdit, onHardDelete, openMutation, router],
    );

    return {
        handleRegisterAction,
        shiftActionPending: openMutation.isPending || closeMutation.isPending,
    };
}
