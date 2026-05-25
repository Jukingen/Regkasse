import { useQuery } from '@tanstack/react-query';

import {
    getDeploymentLicenseStatus,
    licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { useI18n } from '@/i18n';
import { getAdminTenantLicense } from '@/features/super-admin/api/adminTenantLicense';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    getLicenseStatusMessage,
    resolveDeploymentLicenseStatus,
    resolveTenantLicenseStatus,
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
    const resolvedTenantId = tenantId ?? currentTenant.tenantId ?? undefined;

    return useQuery({
        queryKey: ['tenant-license-status', resolvedTenantId, textLocale],
        queryFn: async () => {
            const response = await getAdminTenantLicense(resolvedTenantId!);
            const status = resolveTenantLicenseStatus(response.status);
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

    return useQuery({
        queryKey: [...licenseQueryKeys.deploymentStatus, textLocale],
        queryFn: async () => {
            const response = await getDeploymentLicenseStatus();
            const status = resolveDeploymentLicenseStatus(response);
            return {
                ...status,
                message: getLicenseStatusMessage(status, 'deployment', t),
            } satisfies LicenseStatus;
        },
    });
};
