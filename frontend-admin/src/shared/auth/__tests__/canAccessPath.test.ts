import { describe, it, expect } from 'vitest';

import { canAccessPath } from '@/shared/auth/canAccessPath';
import { PERMISSIONS } from '@/shared/auth/permissions';
import {
    CASHIER_ADMIN_PERMISSIONS,
    MANAGER_ADMIN_PERMISSIONS,
    MANAGER_FORBIDDEN_MENU_KEYS,
    MANAGER_REQUIRED_MENU_KEYS,
} from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

describe('canAccessPath', () => {
    it('allows Manager with finanzonline.manage to open Sonderbelege', () => {
        expect(canAccessPath('/rksv/sonderbelege', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
    });

    it('denies Cashier without finanzonline.manage', () => {
        expect(
            canAccessPath('/rksv/sonderbelege', [
                PERMISSIONS.PAYMENT_VIEW,
                PERMISSIONS.REPORT_VIEW,
            ]),
        ).toBe(false);
    });

    it('allows dashboard for any user with permission claims', () => {
        expect(canAccessPath('/dashboard', [PERMISSIONS.PAYMENT_VIEW])).toBe(true);
    });

    it('denies unknown paths without route mapping', () => {
        expect(canAccessPath('/unknown/path', [PERMISSIONS.PAYMENT_VIEW])).toBe(false);
    });

    it('allows all MANAGER_REQUIRED_MENU_KEYS with manager admin permissions', () => {
        const perms = [...MANAGER_ADMIN_PERMISSIONS];
        const denied = MANAGER_REQUIRED_MENU_KEYS.filter((path) => !canAccessPath(path, perms));
        expect(
            denied,
            `Route guard blocks manager oversight paths: ${denied.join(', ')}`,
        ).toEqual([]);
    });

    it('denies MANAGER_FORBIDDEN_MENU_KEYS for manager admin permissions', () => {
        const perms = [...MANAGER_ADMIN_PERMISSIONS];
        const allowed = MANAGER_FORBIDDEN_MENU_KEYS.filter((path) => canAccessPath(path, perms));
        expect(
            allowed,
            `Route guard should block manager-forbidden paths: ${allowed.join(', ')}`,
        ).toEqual([]);
    });

    it('allows signature-debug forensics paths via payment.view / sale.view', () => {
        expect(canAccessPath('/rksv/belegcheck', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
        expect(canAccessPath('/payments', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
        expect(canAccessPath('/receipts', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
    });

    it('denies platform admin paths even when cash_register.view is present', () => {
        expect(
            canAccessPath('/admin/tenants', [
                PERMISSIONS.CASHREGISTER_VIEW,
                PERMISSIONS.USER_VIEW,
            ]),
        ).toBe(false);
    });

    it('Cashier admin permissions match required cashier routes only', () => {
        const perms = [...CASHIER_ADMIN_PERMISSIONS];
        expect(canAccessPath('/payments', perms)).toBe(true);
        expect(canAccessPath('/receipts', perms)).toBe(false);
        expect(canAccessPath('/kassenverwaltung', perms)).toBe(false);
    });
});
