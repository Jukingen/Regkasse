import { useQuery } from '@tanstack/react-query';

import {
    getDeploymentLicenseStatus,
    licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useAuthorizationGate, useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { useI18n } from '@/i18n';
import { getTenantLicense } from '@/features/license/api/tenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    getLicenseStatusMessage,
    resolveDeploymentLicenseStatus,
    resolveTenantLicenseStatus,
    resolveTenantRowLicenseStatus,
    type LicenseStatusKind,
} from '@/features/license/utils/licenseStatus';

export interface LicenseStatus {
    kind: LicenseStatusKind;
    daysRemaining: number;
    daysExpired: number;
    canWrite: boolean;
    canManageUsers: boolean;
    canAccess: boolean;
    message: string;
}

export const useTenantLicenseStatus = (tenantId?: string) => {
    const { t, textLocale } = useI18n();
    const currentTenant = useCurrentTenant();
    const { isAuthorized: canFetchTenantLicense } = useAuthorizationGate({
        requiredPermission: PERMISSIONS.LICENSE_MANAGE,
    });
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? undefined;
    const shouldFetchRemote =
        canFetchTenantLicense && Boolean(resolvedTenantId && currentTenant.isRealTenantSlug);

    return useQuery({
        queryKey: [
            'tenant-license-status',
            resolvedTenantId,
            textLocale,
            shouldFetchRemote ? 'remote' : 'local',
        ],
        queryFn: async () => {
            if (shouldFetchRemote && resolvedTenantId) {
                const response = await getTenantLicense(resolvedTenantId);
                const status = resolveTenantLicenseStatus(response.status);
                return {
                    ...status,
                    message: getLicenseStatusMessage(status, 'tenant', t),
                } satisfies LicenseStatus;
            }

            const status = resolveTenantRowLicenseStatus({
                licenseValidUntilUtc: currentTenant.licenseValidUntilUtc,
                licenseKey: currentTenant.licenseKey,
                licenseDaysRemaining: currentTenant.licenseDaysRemaining,
            });
            return {
                ...status,
                message: getLicenseStatusMessage(status, 'tenant', t),
            } satisfies LicenseStatus;
        },
        enabled: Boolean(resolvedTenantId && currentTenant.hasAuthToken),
    });
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
