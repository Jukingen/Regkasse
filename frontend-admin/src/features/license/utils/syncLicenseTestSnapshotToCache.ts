import type { QueryClient } from '@tanstack/react-query';

import {
    tenantLicenseUnifiedQueryKeyFor,
    type LicensePublicStatusDto,
} from '@/api/manual/adminLicense';
import type { LicenseTestSnapshot, LicenseTestTenantStatus } from '@/features/license/api/licenseTest';
import type { TenantLicenseOverview } from '@/features/license/api/tenantLicense';
import { tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import { mapTenantLicenseOverviewToPublicStatus } from '@/features/license/utils/mapTenantLicenseOverviewToPublicStatus';

function mapLicenseTestTenantToOverview(tenant: LicenseTestTenantStatus): TenantLicenseOverview {
    return {
        status: {
            kind: tenant.status,
            licenseKey: tenant.licenseKey ?? null,
            validUntilUtc: tenant.validUntilUtc ?? null,
            daysRemaining: tenant.daysRemaining,
            tier: null,
            features: [],
        },
        history: [],
    };
}

/** Push test-panel snapshot into TanStack caches for instant header / license page updates. */
export function syncLicenseTestSnapshotToCache(
    queryClient: QueryClient,
    snapshot: LicenseTestSnapshot,
): void {
    const tenant = snapshot.tenant;
    if (!tenant?.tenantId) {
        return;
    }

    const overview = mapLicenseTestTenantToOverview(tenant);
    const publicStatus: LicensePublicStatusDto = mapTenantLicenseOverviewToPublicStatus(overview);

    queryClient.setQueryData(tenantLicenseQueryKeys.detail(tenant.tenantId), overview);
    queryClient.setQueryData(tenantLicenseUnifiedQueryKeyFor(tenant.tenantId, 'public'), publicStatus);
    queryClient.setQueryData(tenantLicenseUnifiedQueryKeyFor(tenant.tenantId, 'admin'), publicStatus);
}
