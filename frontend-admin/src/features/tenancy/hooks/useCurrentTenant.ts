'use client';

import type { CurrentTenant } from '@/features/tenancy/hooks/useCurrentTenantState';
import { useTenantProviderValue } from '@/features/tenancy/providers/TenantProvider';

export type { CurrentTenant };

/**
 * Resolved mandant snapshot from {@link TenantProvider} (shared across all admin pages).
 */
export function useCurrentTenant(): CurrentTenant {
  const currentTenant = useTenantProviderValue();
  if (!currentTenant) {
    throw new Error('useCurrentTenant must be used within TenantProvider');
  }
  return currentTenant;
}
