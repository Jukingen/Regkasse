'use client';

import {
  type LicensePublicStatusDto,
  type TenantLicenseQuerySource,
  getTenantLicensePublicStatus,
  tenantLicenseUnifiedQueryKeyFor,
} from '@/api/manual/adminLicense';
import { isDevelopment } from '@/features/auth/services/devTenant';
import { getTenantLicense } from '@/features/license/api/tenantLicense';
import { mapTenantLicenseOverviewToPublicStatus } from '@/features/license/utils/mapTenantLicenseOverviewToPublicStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { usePermissions } from '@/hooks/usePermissions';
import { formatGermanDateTime } from '@/lib/dateFormatter';
import { PERMISSIONS } from '@/shared/auth/permissions';

export {
  type TenantLicenseQuerySource,
  tenantLicenseUnifiedQueryKey,
  tenantLicenseUnifiedQueryKeyFor,
} from '@/api/manual/adminLicense';

const TENANT_LICENSE_STALE_MS = 5 * 60 * 1000;
const DEV_TENANT_LICENSE_STALE_MS = 30 * 1000;

type UseTenantLicenseOptions = {
  enabled?: boolean;
};

/** Unified license snapshot plus German display strings (`DD.MM.YYYY HH:mm`). */
export type TenantLicenseViewModel = LicensePublicStatusDto & {
  validUntilFormatted: string;
};

/** Maps API ISO timestamps to fixed de-AT display fields for UI consumers. */
export function toTenantLicenseViewModel(status: LicensePublicStatusDto): TenantLicenseViewModel {
  return {
    ...status,
    validUntilFormatted: formatGermanDateTime(status.validUntil),
  };
}

async function fetchTenantLicensePublicStatus(
  tenantId: string | null,
  useAdminTenantLicense: boolean
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
 * `data` includes {@link TenantLicenseViewModel.validUntilFormatted} (`DD.MM.YYYY HH:mm`).
 * Formatting is applied via `select` so direct cache writes (e.g. license test panel) stay consistent.
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

  return useAuthorizedQuery<LicensePublicStatusDto, Error, TenantLicenseViewModel>({
    queryKey: tenantLicenseUnifiedQueryKeyFor(resolvedTenantId, querySource),
    queryFn: () => fetchTenantLicensePublicStatus(resolvedTenantId, useAdminTenantLicense),
    select: toTenantLicenseViewModel,
    requiredPermission: [
      PERMISSIONS.LICENSE_VIEW,
      PERMISSIONS.LICENSE_MANAGE,
      PERMISSIONS.SYSTEM_CRITICAL,
    ],
    enabled:
      explicitlyEnabled &&
      Boolean(resolvedTenantId && currentTenant.hasAuthToken && currentTenant.isRealTenantSlug),
    staleTime: devMode ? DEV_TENANT_LICENSE_STALE_MS : TENANT_LICENSE_STALE_MS,
    refetchOnMount: true,
    refetchOnWindowFocus: devMode ? 'always' : true,
    refetchOnReconnect: true,
  });
}
