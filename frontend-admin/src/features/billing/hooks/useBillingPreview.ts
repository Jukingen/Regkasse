'use client';

import { billingApi } from '@/features/billing/api/billingApi';

export function useBillingPreview() {
  return billingApi.usePreview();
}
