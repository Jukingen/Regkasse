'use client';

import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { getTenantLicense, tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';

type UseTenantLicenseDetailOptions = {
    enabled?: boolean;
};

/** Admin mandant license overview (history, tier, key) — not the POS status contract. */
export function useTenantLicenseDetail(
    tenantId?: string,
    options?: UseTenantLicenseDetailOptions,
) {
    const currentTenant = useCurrentTenant();
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? '';
    const explicitlyEnabled = options?.enabled ?? true;

    return useAuthorizedQuery({
        queryKey: tenantLicenseQueryKeys.detail(resolvedTenantId),
        queryFn: () => getTenantLicense(resolvedTenantId),
        requiredPermission: PERMISSIONS.LICENSE_MANAGE,
        enabled:
            explicitlyEnabled
            && Boolean(resolvedTenantId && currentTenant.isRealTenantSlug),
        refetchOnMount: true,
        refetchOnWindowFocus: true,
    });
}

/** @deprecated Use {@link useTenantLicenseDetail} for admin overview or {@link useTenantLicense} from `@/hooks/useTenantLicense` for status. */
export { useTenantLicenseDetail as useTenantLicense };
