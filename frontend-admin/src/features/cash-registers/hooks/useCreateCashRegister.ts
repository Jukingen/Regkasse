'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getGetApiCashRegisterQueryKey } from '@/api/generated/cash-register/cash-register';
import {
  type CreateCashRegisterRequest,
  cashRegisterListQueryKey,
  createCashRegister,
} from '@/features/cash-registers/api/cashRegisters';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export type UseCreateCashRegisterOptions = {
  onSuccess?: () => void;
};

export function useCreateCashRegister(options: UseCreateCashRegisterOptions = {}) {
  const { message } = useAntdApp();

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
