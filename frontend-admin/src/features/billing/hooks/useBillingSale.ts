'use client';

import { billingApi } from '@/features/billing/api/billingApi';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useBillingSale(id: string | undefined) {
  const canAccess = useBillingAccess();
  const saleId = id?.trim() ?? '';

  return billingApi.useGet(saleId, {
    query: { enabled: canAccess && saleId.length > 0 },
  });
}
