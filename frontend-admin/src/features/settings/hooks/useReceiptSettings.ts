'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  type ReceiptSettings,
  fetchReceiptSettings,
  updateReceiptSettings,
} from '@/features/settings/api/receiptSettingsApi';

export const RECEIPT_SETTINGS_KEYS = {
  all: ['admin-receipt-settings'] as const,
};

export function useReceiptSettings() {
  return useQuery({
    queryKey: RECEIPT_SETTINGS_KEYS.all,
    queryFn: fetchReceiptSettings,
    staleTime: 60_000,
  });
}

export function useUpdateReceiptSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (thankYouMessage: string) => updateReceiptSettings(thankYouMessage),
    onSuccess: (data: ReceiptSettings) => {
      queryClient.setQueryData(RECEIPT_SETTINGS_KEYS.all, data);
    },
  });
}
