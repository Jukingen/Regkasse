'use client';

import { useTenant } from '@/hooks/useTenant';
import type { Tenant } from '@/hooks/useTenant';

export type TsePageTenant = {
  tenant: Tenant | null;
  tenantId: string | undefined;
  tenantName: string | undefined;
  isReady: boolean;
  isLoading: boolean;
};

/** Shared mandant context for Super Admin TSE ops pages (header / TenantGuard selection). */
export function useTsePageTenant(): TsePageTenant {
  const { tenant, isLoading } = useTenant();
  const tenantId = tenant?.id;
  return {
    tenant,
    tenantId,
    tenantName: tenant?.name,
    isReady: Boolean(tenantId),
    isLoading,
  };
}
