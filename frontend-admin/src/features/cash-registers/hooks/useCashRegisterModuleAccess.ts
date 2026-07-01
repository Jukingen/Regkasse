'use client';

import { useMemo } from 'react';

import {
    useTenantLicenseStatus,
    type LicenseStatus,
} from '@/features/license/hooks/useLicenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { usePermissions } from '@/shared/auth/usePermissions';

export function isTenantLicenseBlockingModule(
    license: LicenseStatus | undefined,
    isSuperAdminUser: boolean,
): boolean {
    if (isSuperAdminUser || !license) {
        return false;
    }

    return !license.canAccess || license.kind === 'no_license' || license.kind === 'lockdown';
}

/**
 * Kassenverwaltung: menu/route uses cash_register.manage; skip data load when tenant license blocks ops.
 */
export function useCashRegisterModuleAccess() {
    const permissions = usePermissions();
    const { tenantId, isSuperAdminUser } = useCurrentTenant();
    const { data: tenantLicense, isLoading: licenseLoading } = useTenantLicenseStatus();

    const licenseBlocksModule = useMemo(
        () => isTenantLicenseBlockingModule(tenantLicense, isSuperAdminUser),
        [isSuperAdminUser, tenantLicense],
    );

    const canAccessPage = permissions.canManageCashRegisters;
    const canViewRegistersForLoad =
        permissions.canViewCashRegisters || permissions.canManageCashRegisters;
    const canLoadRegisters =
        canAccessPage &&
        canViewRegistersForLoad &&
        (isSuperAdminUser || !licenseBlocksModule);

    return {
        ...permissions,
        tenantId,
        isSuperAdminUser,
        tenantLicense,
        licenseLoading,
        licenseBlocksModule,
        canAccessPage,
        canLoadRegisters,
    };
}
