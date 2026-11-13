'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type UpdatePaymentGatewaySettingsPayload,
  fetchPaymentGatewaySettings,
  updatePaymentGatewaySettings,
} from '@/features/settings/api/paymentGatewaySettingsApi';

export const PAYMENT_GATEWAY_SETTINGS_QUERY_KEY = ['admin', 'settings', 'payment-gateway'] as const;

export function usePaymentGatewaySettings() {
  return useQuery({
    queryKey: PAYMENT_GATEWAY_SETTINGS_QUERY_KEY,
    queryFn: fetchPaymentGatewaySettings,
  });
}

export function useUpdatePaymentGatewaySettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (payload: UpdatePaymentGatewaySettingsPayload) =>
      updatePaymentGatewaySettings(payload),
    onSuccess: (data) => {
      queryClient.setQueryData(PAYMENT_GATEWAY_SETTINGS_QUERY_KEY, data);
    },
  });
}
