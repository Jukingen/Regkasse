'use client';

import { useQueryClient } from '@tanstack/react-query';

import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';

export function useCancelLicenseSale() {
  const queryClient = useQueryClient();

  return billingApi.useCancel({
    mutation: {
      onSuccess: (_data, variables) => {
        void queryClient.invalidateQueries({
          queryKey: billingQueryKeys.salesDetail(variables.id),
        });
        void queryClient.invalidateQueries({ queryKey: billingQueryKeys.sales() });
        void queryClient.invalidateQueries({ queryKey: billingQueryKeys.stats() });
      },
    },
  });
}
