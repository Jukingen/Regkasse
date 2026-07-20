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

    it('allows Super Admin platform hub /admin with system.critical (Mandant auswählen)', () => {
        expect(canAccessPath('/admin', [PERMISSIONS.SYSTEM_CRITICAL])).toBe(true);
        expect(canAccessPath('/admin', [...MANAGER_ADMIN_PERMISSIONS])).toBe(false);
    });

    it('Cashier admin permissions match required cashier routes only', () => {
        const perms = [...CASHIER_ADMIN_PERMISSIONS];
        expect(canAccessPath('/payments', perms)).toBe(true);
        expect(canAccessPath('/receipts', perms)).toBe(false);
        expect(canAccessPath('/kassenverwaltung', perms)).toBe(false);
        expect(canAccessPath('/admin/license', perms)).toBe(false);
    });

    it('Manager can access /admin/license with license.manage', () => {
        expect(canAccessPath('/admin/license', [...MANAGER_ADMIN_PERMISSIONS])).toBe(true);
    });

    it('Manager can access staff hub sub-routes (user.view / report.view / shift.view, not staff.*)', () => {
        const perms = [...MANAGER_ADMIN_PERMISSIONS];
        expect(canAccessPath('/staff', perms)).toBe(true);
        expect(canAccessPath('/staff/list', perms)).toBe(true);
        expect(canAccessPath('/staff/performance', perms)).toBe(true);
        expect(canAccessPath('/staff/shifts', perms)).toBe(true);
    });

    it('unlisted /staff/* paths inherit /staff hub guard via longest-prefix match', () => {
        const perms = [...MANAGER_ADMIN_PERMISSIONS];
        // No App Router pages at these paths; guard still allows (Next.js 404). CRUD lives at /admin/users.
        expect(canAccessPath('/staff/activity', perms)).toBe(true);
        expect(canAccessPath('/staff/create', perms)).toBe(true);
        expect(canAccessPath('/staff/edit/abc', perms)).toBe(true);
    });

    it('Manager can access tenant-scoped digital preview and orders deep links', () => {
        const perms = [...MANAGER_ADMIN_PERMISSIONS];
        const tenantId = '11111111-1111-1111-1111-111111111111';
        expect(canAccessPath(`/tenant/${tenantId}/website-preview`, perms)).toBe(true);
        expect(canAccessPath(`/tenant/${tenantId}/orders`, perms)).toBe(true);
        expect(canAccessPath(`/tenant/${tenantId}/digital`, perms)).toBe(true);
        expect(canAccessPath(`/tenant/${tenantId}/data-management`, perms)).toBe(true);
        expect(canAccessPath('/settings/data-management', perms)).toBe(true);
        expect(canAccessPath(`/tenant/${tenantId}/customize`, perms)).toBe(false);
        expect(canAccessPath(`/tenant/${tenantId}/domain`, perms)).toBe(false);
    });

    it('Super Admin data-management overview requires system.critical', () => {
        expect(canAccessPath('/admin/data-management', [PERMISSIONS.SYSTEM_CRITICAL])).toBe(true);
        expect(canAccessPath('/admin/data-management', [...MANAGER_ADMIN_PERMISSIONS])).toBe(false);
    });

    it('staff list requires user.view only', () => {
        expect(canAccessPath('/staff/list', [PERMISSIONS.USER_VIEW])).toBe(true);
        expect(canAccessPath('/staff/list', [PERMISSIONS.REPORT_VIEW])).toBe(false);
    });
});
