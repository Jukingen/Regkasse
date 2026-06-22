'use client';

import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { getTenantLicense, tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';

export function useTenantLicense(tenantId?: string) {
    const currentTenant = useCurrentTenant();
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? '';

    return useAuthorizedQuery({
        queryKey: tenantLicenseQueryKeys.detail(resolvedTenantId),
        queryFn: () => getTenantLicense(resolvedTenantId),
        requiredPermission: PERMISSIONS.LICENSE_MANAGE,
        enabled: Boolean(resolvedTenantId && currentTenant.isRealTenantSlug),
    });
}
