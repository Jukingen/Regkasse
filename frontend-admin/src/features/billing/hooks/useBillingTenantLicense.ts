'use client';

import { billingApi } from '@/features/billing/api/billingApi';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';

export function useBillingTenantLicense(tenantId?: string, enabled = true) {
  const canAccess = useBillingAccess();
  const id = tenantId?.trim() ?? '';

  return billingApi.useTenantLicense(id, {
    query: {
      enabled: canAccess && !!id && enabled,
      queryKey: id ? billingQueryKeys.tenantLicense(id) : undefined,
    },
  });
}
