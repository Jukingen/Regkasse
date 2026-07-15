'use client';

import {
    getTenantLicensePublicStatus,
    tenantLicenseUnifiedQueryKeyFor,
    type LicensePublicStatusDto,
    type TenantLicenseQuerySource,
} from '@/api/manual/adminLicense';
import { getTenantLicense } from '@/features/license/api/tenantLicense';
import { mapTenantLicenseOverviewToPublicStatus } from '@/features/license/utils/mapTenantLicenseOverviewToPublicStatus';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

export {
    tenantLicenseUnifiedQueryKey,
    tenantLicenseUnifiedQueryKeyFor,
    type TenantLicenseQuerySource,
} from '@/api/manual/adminLicense';

const TENANT_LICENSE_STALE_MS = 5 * 60 * 1000;
const DEV_TENANT_LICENSE_STALE_MS = 30 * 1000;

type UseTenantLicenseOptions = {
    enabled?: boolean;
};

async function fetchTenantLicensePublicStatus(
    tenantId: string | null,
    useAdminTenantLicense: boolean,
): Promise<LicensePublicStatusDto> {
    if (useAdminTenantLicense && tenantId) {
        const overview = await getTenantLicense(tenantId);
        return mapTenantLicenseOverviewToPublicStatus(overview);
    }
    return getTenantLicensePublicStatus(tenantId);
}

/**
 * Unified mandant license read model.
 *
 * - **Super Admin** (`system.critical`) with resolved tenant: `GET /api/admin/tenants/{id}/license`
 *   (persisted DB row — no Development enforcement overlay).
 * - **Manager / POS contract**: `GET /api/license/status?tenantId=` (mandant overlay).
 *
 * Auto-refresh: refetches on mount and when the browser tab regains focus (always in Development
 * so test-panel changes appear without a manual reload).
 */
export function useTenantLicense(tenantId?: string, options?: UseTenantLicenseOptions) {
    const currentTenant = useCurrentTenant();
    const { hasPermission } = usePermissions();
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? null;
    const useAdminTenantLicense =
        hasPermission(PERMISSIONS.SYSTEM_CRITICAL) && Boolean(resolvedTenantId);
    const querySource: TenantLicenseQuerySource = useAdminTenantLicense ? 'admin' : 'public';
    const explicitlyEnabled = options?.enabled ?? true;
    const devMode = isDevelopment();

    return useAuthorizedQuery<LicensePublicStatusDto>({
        queryKey: tenantLicenseUnifiedQueryKeyFor(resolvedTenantId, querySource),
        queryFn: () =>
            fetchTenantLicensePublicStatus(resolvedTenantId, useAdminTenantLicense),
        requiredPermission: [
            PERMISSIONS.LICENSE_VIEW,
            PERMISSIONS.LICENSE_MANAGE,
            PERMISSIONS.SYSTEM_CRITICAL,
        ],
        enabled:
            explicitlyEnabled
            && Boolean(resolvedTenantId && currentTenant.hasAuthToken && currentTenant.isRealTenantSlug),
        staleTime: devMode ? DEV_TENANT_LICENSE_STALE_MS : TENANT_LICENSE_STALE_MS,
        refetchOnMount: true,
        refetchOnWindowFocus: devMode ? 'always' : true,
        refetchOnReconnect: true,
    });
}