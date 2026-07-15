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
    canWrite: boolean;
    canManageUsers: boolean;
    canAccess: boolean;
    message: string;
}

function mapTenantLicenseStatus(
    t: ReturnType<typeof useI18n>['t'],
    currentTenant: ReturnType<typeof useCurrentTenant>,
    remoteStatus: ReturnType<typeof resolveTenantLicenseFromPublicStatus> | null,
): LicenseStatus {
    const status =
        remoteStatus ??
        resolveTenantRowLicenseStatus({
            licenseValidUntilUtc: currentTenant.licenseValidUntilUtc,
            licenseKey: currentTenant.licenseKey,
            licenseDaysRemaining: currentTenant.licenseDaysRemaining,
        });

    return {
        ...status,
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
            );
        }

        if (query.isLoading || query.isFetching) {
            return undefined;
        }

        // Remote query ran but returned nothing — do not fall back to stale switcher row.
        if (resolvedTenantId && currentTenant.isRealTenantSlug && currentTenant.hasAuthToken) {
            return undefined;
        }

        return mapTenantLicenseStatus(t, currentTenant, null);
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
    } as UseQueryResult<LicenseStatus>;
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
                message: getLicenseStatusMessage(status, 'deployment', t),
            } satisfies LicenseStatus;
        },
        requiredPermission: PERMISSIONS.SETTINGS_VIEW,
    });
};
