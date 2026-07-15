'use client';

import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    getMandantLicenseHistory,
    licenseHistoryQueryKeys,
} from '@/features/license/api/licenseHistory';

export function useLicenseHistory(tenantId?: string) {
    const currentTenant = useCurrentTenant();
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? '';

    return useAuthorizedQuery({
        queryKey: licenseHistoryQueryKeys.detail(resolvedTenantId),
        queryFn: () => getMandantLicenseHistory(resolvedTenantId),
        requiredPermission: PERMISSIONS.LICENSE_MANAGE,
        enabled: Boolean(resolvedTenantId && currentTenant.isRealTenantSlug),
        staleTime: 60_000,
        select: (response) => response.items ?? [],
    });
}
