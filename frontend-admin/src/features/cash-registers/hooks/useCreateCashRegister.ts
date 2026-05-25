'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';

import {
    cashRegisterListQueryKey,
    createCashRegister,
    type CreateCashRegisterRequest,
} from '@/features/cash-registers/api/cashRegisters';
import { getGetApiCashRegisterQueryKey } from '@/api/generated/cash-register/cash-register';
import { useI18n } from '@/i18n';

export type UseCreateCashRegisterOptions = {
    onSuccess?: () => void;
};

export function useCreateCashRegister(options: UseCreateCashRegisterOptions = {}) {
    const { t } = useI18n();
    const queryClient = useQueryClient();
    const { onSuccess } = options;

    return useMutation({
        mutationFn: (body: CreateCashRegisterRequest) => createCashRegister(body),
        onSuccess: () => {
            void queryClient.invalidateQueries({ queryKey: cashRegisterListQueryKey });
            void queryClient.invalidateQueries({ queryKey: ['admin', 'cash-registers', 'list'] });
            void queryClient.invalidateQueries({ queryKey: getGetApiCashRegisterQueryKey() });
            message.success(t('cashRegisters.create.success'));
            onSuccess?.();
        },
    });
}
