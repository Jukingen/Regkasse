import { describe, expect, it } from 'vitest';

import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    CASHIER_ADMIN_PERMISSIONS,
    MANAGER_ADMIN_PERMISSIONS,
} from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';
import {
    canAccessLicenseAdminPage,
    canManageTenantLicense,
    canViewDeploymentLicenseSection,
} from '@/features/license/utils/licenseAccess';
import { resolveLicensePageSectionVisibility } from '@/features/license/utils/licensePageVisibility';

const managerContext = {
    hasTenantContext: true,
    role: 'Manager',
    isSuperAdminPlatformMode: false,
};

const superAdminContext = {
    hasTenantContext: true,
    role: 'SuperAdmin',
    isSuperAdminPlatformMode: false,
};

describe('license access (requirements §7–8)', () => {
    it('Manager can manage tenant license with tenant context', () => {
        const user = { permissions: [...MANAGER_ADMIN_PERMISSIONS], role: 'Manager' };
        expect(canManageTenantLicense(user, managerContext)).toBe(true);
        expect(canAccessLicenseAdminPage(user)).toBe(true);
    });

    it('Manager cannot see deployment license section', () => {
        const user = { permissions: [...MANAGER_ADMIN_PERMISSIONS], role: 'Manager' };
        expect(canViewDeploymentLicenseSection(user)).toBe(false);
    });

    it('Manager without tenant context cannot manage tenant license', () => {
        const user = { permissions: [...MANAGER_ADMIN_PERMISSIONS], role: 'Manager' };
        expect(
            canManageTenantLicense(user, {
                hasTenantContext: false,
                role: 'Manager',
            }),
        ).toBe(false);
    });

    it('Super Admin on platform host sees deployment and all-tenant overview tabs', () => {
        const user = {
            permissions: [PERMISSIONS.SETTINGS_MANAGE, PERMISSIONS.LICENSE_MANAGE],
            role: 'SuperAdmin',
        };
        const visibility = resolveLicensePageSectionVisibility(user, {
            hasTenantContext: false,
            role: 'SuperAdmin',
            isSuperAdminPlatformMode: true,
        });
        expect(visibility.showAllTenantLicensesSection).toBe(true);
        expect(visibility.showDeploymentSection).toBe(true);
        expect(visibility.showTenantLicenseSection).toBe(false);
    });

    it('Super Admin impersonating tenant sees own sections without all-tenant overview', () => {
        const user = {
            permissions: [PERMISSIONS.SETTINGS_MANAGE, PERMISSIONS.LICENSE_MANAGE],
            role: 'SuperAdmin',
        };
        const visibility = resolveLicensePageSectionVisibility(user, superAdminContext);
        expect(visibility.showAllTenantLicensesSection).toBe(false);
        expect(visibility.showDeploymentSection).toBe(true);
        expect(visibility.showTenantLicenseSection).toBe(true);
    });

    it('Manager sees only own tenant license section, not overview or deployment', () => {
        const user = { permissions: [...MANAGER_ADMIN_PERMISSIONS], role: 'Manager' };
        const visibility = resolveLicensePageSectionVisibility(user, managerContext);
        expect(visibility.showAllTenantLicensesSection).toBe(false);
        expect(visibility.showDeploymentSection).toBe(false);
        expect(visibility.showTenantLicenseSection).toBe(true);
    });

    it('Cashier cannot access license admin page', () => {
        const user = { permissions: [...CASHIER_ADMIN_PERMISSIONS], role: 'Cashier' };
        expect(canAccessLicenseAdminPage(user)).toBe(false);
        expect(canManageTenantLicense(user, managerContext)).toBe(false);
        expect(canViewDeploymentLicenseSection(user)).toBe(false);
    });

    it('settings.manage alone does not grant deployment section without Super Admin role', () => {
        const user = { permissions: [PERMISSIONS.SETTINGS_MANAGE], role: 'Manager' };
        expect(canViewDeploymentLicenseSection(user)).toBe(true);
        const visibility = resolveLicensePageSectionVisibility(user, managerContext);
        expect(visibility.showDeploymentSection).toBe(false);
        expect(canManageTenantLicense(user, managerContext)).toBe(true);
    });
});
