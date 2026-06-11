'use client';

import { useMemo } from 'react';

import {
    useDeploymentLicenseStatus,
    useTenantLicenseStatus,
    type LicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

export type EffectiveLicenseStatus = LicenseStatus & {
    isReadOnly: boolean;
};

function resolveEffectiveLicenseStatus(
    tenantLicense: LicenseStatus | undefined,
    deploymentLicense: LicenseStatus | undefined,
    isRealTenantSlug: boolean,
): LicenseStatus | null {
    if (tenantLicense && tenantLicense.kind !== 'active' && isRealTenantSlug) {
        return tenantLicense;
    }
    if (deploymentLicense && deploymentLicense.kind !== 'active') {
        return deploymentLicense;
    }
    if (tenantLicense) {
        return tenantLicense;
    }
    return deploymentLicense ?? null;
}

export function useLicense() {
    const tenant = useCurrentTenant();
    const { data: tenantLicense } = useTenantLicenseStatus();
    const { data: deploymentLicense } = useDeploymentLicenseStatus();

    const licenseStatus = useMemo((): EffectiveLicenseStatus | null => {
        const resolved = resolveEffectiveLicenseStatus(
            tenantLicense,
            deploymentLicense,
            tenant.isRealTenantSlug,
        );
        if (!resolved) {
            return null;
        }

        const isReadOnly = !resolved.canWrite && resolved.canAccess;
        return { ...resolved, isReadOnly };
    }, [tenantLicense, deploymentLicense, tenant.isRealTenantSlug]);

    return { licenseStatus };
}
