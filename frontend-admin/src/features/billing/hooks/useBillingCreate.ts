'use client';

import { useQueryClient } from '@tanstack/react-query';

import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';

export function useBillingCreate() {
  const queryClient = useQueryClient();

  return billingApi.useCreate({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: billingQueryKeys.sales() });
        void queryClient.invalidateQueries({ queryKey: billingQueryKeys.stats() });
      },
    },
  });
}
