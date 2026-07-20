import { useMemo } from 'react';
import type { UseQueryResult } from '@tanstack/react-query';

import { useI18n } from '@/i18n';
import { useTenantLicense } from '@/hooks/useTenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    getLicenseStatusMessage,
    resolveDeploymentLicenseStatus,
    resolveTenantLicenseFromPublicStatus,
    resolveTenantRowLicenseStatus,
    type LicenseStatusKind,
} from '@/features/license/utils/licenseStatus';
import { TENANT_GRACE_PERIOD_DAYS, clampTenantGraceRemaining } from '@/features/license/constants/licenseGracePeriod';
import {
    getDeploymentLicenseStatus,
    licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

export interface LicenseStatus {
    kind: LicenseStatusKind;
    daysRemaining: number;
    daysExpired: number;
    /** Remaining grace days when kind is grace_write; otherwise 0. */
    daysRemainingInGrace: number;
    canWrite: boolean;
    canManageUsers: boolean;
    canAccess: boolean;
    /** True when license validity is past (grace or lockdown). */
    isExpired: boolean;
    /** True when past grace — POS locked. */
    isLocked: boolean;
    /** ISO lock date from API when available. */
    lockDate: string | null;
    message: string;
}

function isExpiredKind(kind: LicenseStatusKind): boolean {
    return (
        kind === 'grace_write' ||
        kind === 'grace_readonly' ||
        kind === 'lockdown' ||
        kind === 'expired'
    );
}

function mapTenantLicenseStatus(
    t: ReturnType<typeof useI18n>['t'],
    currentTenant: ReturnType<typeof useCurrentTenant>,
    remoteStatus: ReturnType<typeof resolveTenantLicenseFromPublicStatus> | null,
    publicDto?: {
        lockDate?: string | null;
        gracePeriodRemaining?: number;
        isLocked?: boolean;
        isExpired?: boolean;
        isInGracePeriod?: boolean;
    } | null,
): LicenseStatus {
    const status =
        remoteStatus ??
        resolveTenantRowLicenseStatus({
            licenseValidUntilUtc: currentTenant.licenseValidUntilUtc,
            licenseKey: currentTenant.licenseKey,
            licenseDaysRemaining: currentTenant.licenseDaysRemaining,
        });

    const isLocked =
        publicDto?.isLocked === true || status.kind === 'lockdown' || status.kind === 'expired';
    const isExpired =
        publicDto?.isExpired === true ||
        publicDto?.isInGracePeriod === true ||
        isExpiredKind(status.kind);
    const daysRemainingInGrace =
        status.kind === 'grace_write'
            ? clampTenantGraceRemaining(
                  typeof publicDto?.gracePeriodRemaining === 'number' &&
                      Number.isFinite(publicDto.gracePeriodRemaining)
                      ? publicDto.gracePeriodRemaining
                      : TENANT_GRACE_PERIOD_DAYS - status.daysExpired,
              )
            : 0;
    const lockDate =
        typeof publicDto?.lockDate === 'string' && publicDto.lockDate.length > 0
            ? publicDto.lockDate
            : null;

    return {
        ...status,
        daysRemainingInGrace,
        isExpired,
        isLocked,
        lockDate,
        message: getLicenseStatusMessage(status, 'tenant', t),
    };
}

export const useTenantLicenseStatus = (tenantId?: string): UseQueryResult<LicenseStatus> => {
    const { t } = useI18n();
    const currentTenant = useCurrentTenant();
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? undefined;
    const query = useTenantLicense(resolvedTenantId);

    const data = useMemo(() => {
        if (!resolvedTenantId || !currentTenant.hasAuthToken) {
            return undefined;
        }

        if (query.data) {
            return mapTenantLicenseStatus(
                t,
                currentTenant,
                resolveTenantLicenseFromPublicStatus(query.data),
                query.data,
            );
        }

        if (query.isLoading || query.isFetching) {
            return undefined;
        }

        // Remote query ran but returned nothing — do not fall back to stale switcher row.
        if (resolvedTenantId && currentTenant.isRealTenantSlug && currentTenant.hasAuthToken) {
            return undefined;
        }

        return mapTenantLicenseStatus(t, currentTenant, null, null);
    }, [
        resolvedTenantId,
        currentTenant,
        query.data,
        query.isLoading,
        query.isFetching,
        t,
    ]);

    return {
        ...query,
        data,
    } as unknown as UseQueryResult<LicenseStatus>;
};

export const useDeploymentLicenseStatus = () => {
    const { t, textLocale } = useI18n();

    return useAuthorizedQuery({
        queryKey: [...licenseQueryKeys.deploymentStatus, textLocale],
        queryFn: async () => {
            const response = await getDeploymentLicenseStatus();
            const status = resolveDeploymentLicenseStatus(response);
            return {
                ...status,
                daysRemainingInGrace:
                    status.kind === 'grace_write'
                        ? Math.max(0, 15 - status.daysExpired)
                        : 0,
                isExpired: status.kind !== 'active' && status.kind !== 'no_license',
                isLocked: status.kind === 'lockdown' || status.kind === 'expired',
                lockDate: null,
                message: getLicenseStatusMessage(status, 'deployment', t),
            } satisfies LicenseStatus;
        },
        requiredPermission: PERMISSIONS.SETTINGS_VIEW,
    });
};
