import type { AuthUser } from '@/shared/auth/types';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import {
    canManageTenantLicense,
    canViewDeploymentLicenseSection,
    type TenantLicenseAccessContext,
} from '@/features/license/utils/licenseAccess';

export type LicensePageSectionVisibility = {
    showAllTenantLicensesSection: boolean;
    showTenantLicenseSection: boolean;
    showDeploymentSection: boolean;
};

export function resolveLicensePageSectionVisibility(
    user: AuthUser | null | undefined,
    tenantContext: TenantLicenseAccessContext,
): LicensePageSectionVisibility {
    const showAllTenantLicensesSection =
        isSuperAdmin(user?.role) && Boolean(tenantContext.isSuperAdminPlatformMode);

    const showTenantLicenseSection =
        canManageTenantLicense(user, tenantContext)
        && tenantContext.hasTenantContext
        && !showAllTenantLicensesSection;

    const showDeploymentSection =
        isSuperAdmin(user?.role) && canViewDeploymentLicenseSection(user);

    return { showAllTenantLicensesSection, showTenantLicenseSection, showDeploymentSection };
}
