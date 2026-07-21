'use client';

import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useTenantReminders(tenantId: string | undefined) {
  const canAccess = useBillingAccess();
  const id = tenantId?.trim() ?? '';

  return billingApi.useReminders(id, {
    query: {
      enabled: canAccess && id.length > 0,
      queryKey: id ? billingQueryKeys.reminders(id) : undefined,
    },
  });
}
