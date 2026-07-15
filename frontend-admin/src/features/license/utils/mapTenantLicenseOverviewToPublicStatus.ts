import type { LicensePublicStatusDto } from '@/api/manual/adminLicense';
import type { TenantLicenseOverview } from '@/features/license/api/tenantLicense';
import { TENANT_GRACE_PERIOD_DAYS } from '@/features/license/constants/licenseGracePeriod';
import { resolveTenantLicenseStatus } from '@/features/license/utils/licenseStatus';

/** Maps Super Admin `GET /api/admin/tenants/{id}/license` to the unified public read model. */
export function mapTenantLicenseOverviewToPublicStatus(
    overview: TenantLicenseOverview,
): LicensePublicStatusDto {
    const status = overview.status;
    const resolved = resolveTenantLicenseStatus({
        validUntilUtc: status.validUntilUtc,
        daysRemaining: status.daysRemaining ?? undefined,
        kind: status.kind,
        licenseKey: status.licenseKey,
    });

    const isInGracePeriod =
        resolved.kind === 'grace_write' || resolved.kind === 'grace_readonly';
    const gracePeriodRemaining =
        resolved.kind === 'grace_write'
            ? Math.max(0, TENANT_GRACE_PERIOD_DAYS - resolved.daysExpired)
            : 0;

    const isExpired = resolved.kind === 'lockdown' || resolved.kind === 'expired';

    return {
        licenseType: isExpired ? 'Expired' : resolved.kind === 'no_license' ? 'Trial' : 'Licensed',
        validUntil: status.validUntilUtc ?? null,
        daysRemaining: resolved.daysRemaining,
        features: status.features ?? [],
        isExpired,
        isValid: resolved.canAccess,
        mode: 'Production',
        canAccess: resolved.canAccess,
        canTransact: resolved.canWrite,
        isInGracePeriod,
        gracePeriodRemaining,
        requiresRenewal: resolved.kind === 'lockdown',
    };
}
