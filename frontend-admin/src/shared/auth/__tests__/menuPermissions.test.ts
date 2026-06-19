import { describe, expect, it } from 'vitest';
import { canShowRksvMenu } from '@/features/auth/constants/roles';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    isMenuItemAllowed,
    isRksvMenuAreaAllowed,
    isRksvRouteKeyAllowed,
} from '@/shared/auth/menuPermissions';
import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

describe('menuPermissions RKSV helpers', () => {
    it('isRksvMenuAreaAllowed uses finanzonline.manage for Manager admin JWT', () => {
        expect(isRksvMenuAreaAllowed([...MANAGER_ADMIN_PERMISSIONS], 'Manager')).toBe(true);
        expect(isRksvMenuAreaAllowed([PERMISSIONS.PAYMENT_VIEW], 'Manager')).toBe(false);
    });

    it('isRksvMenuAreaAllowed falls back to role when permissions empty', () => {
        expect(isRksvMenuAreaAllowed(undefined, 'Manager')).toBe(true);
        expect(isRksvMenuAreaAllowed([], 'Manager')).toBe(true);
        expect(isRksvMenuAreaAllowed(undefined, 'Cashier')).toBe(false);
        expect(canShowRksvMenu('Manager')).toBe(true);
    });

    it('isRksvRouteKeyAllowed checks per-route permissions when claims exist', () => {
        expect(
            isRksvRouteKeyAllowed('/rksv/compliance', [...MANAGER_ADMIN_PERMISSIONS], 'Manager'),
        ).toBe(true);
        expect(
            isRksvRouteKeyAllowed('/rksv/compliance', [PERMISSIONS.PAYMENT_VIEW], 'Manager'),
        ).toBe(false);
        expect(isMenuItemAllowed('/rksv/compliance', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
    });

    it('isRksvRouteKeyAllowed uses role fallback for legacy sessions', () => {
        expect(isRksvRouteKeyAllowed('/rksv/operations', undefined, 'Manager')).toBe(true);
        expect(isRksvRouteKeyAllowed('/rksv/operations', undefined, 'Cashier')).toBe(false);
    });
});
