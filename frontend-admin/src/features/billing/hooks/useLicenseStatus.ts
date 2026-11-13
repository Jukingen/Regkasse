'use client';

import { billingApi } from '@/features/billing/api/billingApi';

export function useLicenseStatus() {
  return billingApi.useLicenseStatus(undefined, {
    query: { queryKey: ['license', 'status'] },
  });
}
